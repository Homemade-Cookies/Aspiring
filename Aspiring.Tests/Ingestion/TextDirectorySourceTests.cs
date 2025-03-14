using Aspire.ChatApp.Services.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Moq;

namespace Aspiring.Tests.Ingestion;

public class TextDirectorySourceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly Mock<IEmbeddingGenerator<string, Embedding<float>>> _mockEmbeddingGenerator;
    private readonly IngestionCacheDbContext _dbContext;
    private static readonly float[] vector = [0.1f, 0.2f];

    public TextDirectorySourceTests()
    {
        // Setup test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testDirectory);

        // Create mock embedding generator
        _mockEmbeddingGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();

        // Setup in-memory database
        var options = new DbContextOptionsBuilder<IngestionCacheDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
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
    public async Task GetNewOrModifiedDocumentsAsync_ShouldReturnNewDocuments()
    {
        // Arrange
        var fileExtensions = new[] { ".md", ".cs" };
        var source = new TextDirectorySource(_testDirectory, fileExtensions);

        var testFile1 = Path.Combine(_testDirectory, "test1.md");
        var testFile2 = Path.Combine(_testDirectory, "test2.cs");

        await File.WriteAllTextAsync(testFile1, "# Test Markdown");
        await File.WriteAllTextAsync(testFile2, "public class Test {}");

        // Act
        var result = await source.GetNewOrModifiedDocumentsAsync(_dbContext.Documents.AsQueryable());

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(result, d => d.Id == "test1.md");
        Assert.Contains(result, d => d.Id == "test2.cs");
    }

    [Fact]
    public async Task GetNewOrModifiedDocumentsAsync_ShouldNotReturnUnchangedDocuments()
    {
        // Arrange
        var fileExtensions = new[] { ".md" };
        var source = new TextDirectorySource(_testDirectory, fileExtensions);

        var testFile = Path.Combine(_testDirectory, "test.md");
        await File.WriteAllTextAsync(testFile, "# Test Markdown");
        var fileVersion = File.GetLastWriteTimeUtc(testFile).ToString("o");

        // Add existing document to db
        _dbContext.Documents.Add(new IngestedDocument
        {
            Id = "test.md",
            SourceId = source.SourceId,
            Version = fileVersion
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await source.GetNewOrModifiedDocumentsAsync(_dbContext.Documents.AsQueryable());

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetNewOrModifiedDocumentsAsync_ShouldReturnModifiedDocuments()
    {
        // Arrange
        var fileExtensions = new[] { ".md" };
        var source = new TextDirectorySource(_testDirectory, fileExtensions);

        var testFile = Path.Combine(_testDirectory, "test.md");
        await File.WriteAllTextAsync(testFile, "# Test Markdown");

        // Add existing document with old version
        _dbContext.Documents.Add(new IngestedDocument
        {
            Id = "test.md",
            SourceId = source.SourceId,
            Version = DateTime.UtcNow.AddDays(-1).ToString("o")
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await source.GetNewOrModifiedDocumentsAsync(_dbContext.Documents.AsQueryable());

        // Assert
        Assert.Single(result);
        Assert.Equal("test.md", result.First().Id);
    }

    [Fact]
    public async Task GetDeletedDocumentsAsync_ShouldReturnDeletedDocuments()
    {
        // Arrange
        var fileExtensions = new[] { ".md" };
        var source = new TextDirectorySource(_testDirectory, fileExtensions);

        // Add document to db that doesn't exist in the directory
        _dbContext.Documents.Add(new IngestedDocument
        {
            Id = "deleted.md",
            SourceId = source.SourceId,
            Version = DateTime.UtcNow.ToString("o")
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await source.GetDeletedDocumentsAsync(_dbContext.Documents.AsQueryable());

        // Assert
        Assert.Single(result);
        Assert.Equal("deleted.md", result.First().Id);
    }

    [Fact]
    public async Task CreateRecordsForDocumentAsync_ShouldCreateRecords()
    {
        // Arrange
        var fileExtensions = new[] { ".cs" };
        var source = new TextDirectorySource(_testDirectory, fileExtensions);

        var testFile = Path.Combine(_testDirectory, "test.cs");
        await File.WriteAllTextAsync(testFile, "public class Test { public void Method() { } }");

        // Setup mock embedding generator
        var testEmbedding = new Embedding<float>(vector);

        var generatedEmbeddings = new GeneratedEmbeddings<Embedding<float>>(new[] { testEmbedding });
        _mockEmbeddingGenerator.Setup(m => m.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedEmbeddings);

        // Act
        var records = await source.CreateRecordsForDocumentAsync(_mockEmbeddingGenerator.Object, "test.cs");

        // Assert
        Assert.NotEmpty(records);
        var record = records.First();
        Assert.Equal("test.cs", record.FileName);
        Assert.Equal(1, record.PageNumber);
        Assert.NotNull(record.Text);
        Assert.Equal(testEmbedding.Vector, record.Vector);
    }
}
