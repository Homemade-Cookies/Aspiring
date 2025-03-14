using Aspire.ChatApp.Services.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Moq;

namespace Aspiring.Tests.Ingestion;

public class CodeDirectorySourceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly Mock<IEmbeddingGenerator<string, Embedding<float>>> _mockEmbeddingGenerator;
    private readonly IngestionCacheDbContext _dbContext;
    private static readonly float[] vector = [0.1f, 0.2f];
    private static readonly float[] vector2 = [0.3f, 0.4f];
    private static readonly float[] vector3 = [0.5f, 0.6f];
    private static readonly float[] vector4 = [0.5f, 0.8f];

    public CodeDirectorySourceTests()
    {
        // Setup test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testDirectory);

        // Create mock embedding generator
        _mockEmbeddingGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();

        // Setup in-memory database
        var options = new DbContextOptionsBuilder<IngestionCacheDbContext>()
            .UseInMemoryDatabase(databaseName: $"CodeTestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new IngestionCacheDbContext(options);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }

        // Clean up database
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetNewOrModifiedDocumentsAsync_ShouldReturnNewCodeFiles()
    {
        // Arrange
        var fileExtensions = new[] { ".cs", ".ts" };
        var source = new CodeDirectorySource(_testDirectory, fileExtensions);

        var testFile1 = Path.Combine(_testDirectory, "Class1.cs");
        var testFile2 = Path.Combine(_testDirectory, "interface.ts");

        await File.WriteAllTextAsync(testFile1, "public class Class1 { public void Method() {} }");
        await File.WriteAllTextAsync(testFile2, "interface User { id: number; name: string; }");

        // Act
        var result = await source.GetNewOrModifiedDocumentsAsync(_dbContext.Documents.AsQueryable());

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(result, d => d.Id == "Class1.cs");
        Assert.Contains(result, d => d.Id == "interface.ts");
    }

    [Fact]
    public async Task GetDeletedDocumentsAsync_ShouldReturnDeletedCodeFiles()
    {
        // Arrange
        var fileExtensions = new[] { ".cs", ".js" };
        var source = new CodeDirectorySource(_testDirectory, fileExtensions);

        // Add document to db that doesn't exist in the directory
        _dbContext.Documents.Add(new IngestedDocument
        {
            Id = "DeletedClass.cs",
            SourceId = source.SourceId,
            Version = DateTime.UtcNow.ToString("o")
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await source.GetDeletedDocumentsAsync(_dbContext.Documents.AsQueryable());

        // Assert
        Assert.Single(result);
        Assert.Equal("DeletedClass.cs", result.First().Id);
    }

    [Fact]
    public async Task CreateRecordsForDocumentAsync_ShouldCreateStructuredRecords()
    {
        // Arrange
        var fileExtensions = new[] { ".cs" };
        var source = new CodeDirectorySource(_testDirectory, fileExtensions);

        var codeContent = @"
            using System;
            
            namespace Test
            {
                public class TestClass
                {
                    private readonly string _name;
                    
                    public TestClass(string name)
                    {
                        _name = name;
                    }
                    
                    public string GetName()
                    {
                        return _name;
                    }
                }
            }
        ";

        var testFile = Path.Combine(_testDirectory, "TestClass.cs");
        await File.WriteAllTextAsync(testFile, codeContent);

        // Setup mock embedding generator to return a unique embedding for each input
        _mockEmbeddingGenerator.Setup(m => m.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> texts, EmbeddingGenerationOptions options, CancellationToken _) =>
            {
                // Generate a unique embedding for each text input using the provided options
                IEnumerable<Embedding<float>> embeddings = texts.Select((_, index) =>
                    new Embedding<float>(new[] { index * 0.1f, index * 0.2f }));

                if (options != null)
                {
                    foreach (var embedding in embeddings)
                    {
                        embedding.AdditionalProperties = options.AdditionalProperties;
                        embedding.ModelId = options.ModelId;
                    }
                }
                var generatedEmbeddings = new GeneratedEmbeddings<Embedding<float>>(embeddings);
                return generatedEmbeddings;
            });

        // Act
        var records = await source.CreateRecordsForDocumentAsync(_mockEmbeddingGenerator.Object, "TestClass.cs");

        // Assert
        Assert.NotEmpty(records);

        // Verify we have at least one class extraction record
        var classRecord = records.FirstOrDefault(r => r.Key.Contains("class", StringComparison.InvariantCultureIgnoreCase));
        Assert.NotNull(classRecord);
        Assert.Contains("TestClass", classRecord.Text, StringComparison.InvariantCultureIgnoreCase);

        // Verify we have at least one method extraction record
        var methodRecords = records.Where(r => r.Key.Contains("method", StringComparison.InvariantCultureIgnoreCase));
        Assert.NotNull(methodRecords);
        Assert.Contains(methodRecords, x => x.Text.Contains("GetName", StringComparison.InvariantCultureIgnoreCase));
    }

    [Fact]
    public async Task ExtractBlock_ShouldExtractBalancedCodeBlocks()
    {
        // This test verifies the code extraction works properly with balanced braces

        // Arrange
        var fileExtensions = new[] { ".cs" };
        var source = new CodeDirectorySource(_testDirectory, fileExtensions);

        var codeContent = @"
            public class NestedClass 
            {
                public void Method1() 
                {
                    if (condition) 
                    {
                        // Nested block
                    }
                }
                
                public void Method2() { return; }
            }
        ";

        var testFile = Path.Combine(_testDirectory, "NestedClass.cs");
        await File.WriteAllTextAsync(testFile, codeContent);

        // Setup mock embedding generator with mock responses
        var mockEmbeddings = new[]
        {
            new Embedding<float>(vector),
            new Embedding<float>(vector2),
            new Embedding<float>(vector3),
            new Embedding<float>(vector4)
        };

        var generatedEmbeddings = new GeneratedEmbeddings<Embedding<float>>(mockEmbeddings);

        _mockEmbeddingGenerator.Setup(m => m.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(generatedEmbeddings);

        // Act
        var records = await source.CreateRecordsForDocumentAsync(_mockEmbeddingGenerator.Object, "NestedClass.cs");

        // Assert
        Assert.NotEmpty(records);

        // Verify class extraction contains both methods
        var classRecord = records.FirstOrDefault(r => r.Key.Contains("class", StringComparison.InvariantCultureIgnoreCase));
        Assert.NotNull(classRecord);
        Assert.Contains("NestedClass", classRecord.Text, StringComparison.InvariantCultureIgnoreCase);

        // Verify we have separate method records
        var methodRecords = records.Where(r => r.Key.Contains("method", StringComparison.InvariantCultureIgnoreCase)).ToList();
        Assert.True(methodRecords.Count >= 2);
        Assert.Contains(methodRecords, r => r.Text.Contains("Method1", StringComparison.InvariantCultureIgnoreCase));
        Assert.Contains(methodRecords, r => r.Text.Contains("Method2", StringComparison.InvariantCultureIgnoreCase));
    }
}
