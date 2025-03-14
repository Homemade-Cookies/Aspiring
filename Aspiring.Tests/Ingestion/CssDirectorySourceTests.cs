using Aspire.ChatApp.Services.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Moq;

namespace Aspiring.Tests.Ingestion;

public class CssDirectorySourceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly Mock<IEmbeddingGenerator<string, Embedding<float>>> _mockEmbeddingGenerator;
    private readonly IngestionCacheDbContext _dbContext;
    private static readonly float[] vector = [0.1f, 0.2f];
    private static readonly float[] vector2 = [0.3f, 0.4f];

    public CssDirectorySourceTests()
    {
        // Setup test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testDirectory);

        // Create mock embedding generator
        _mockEmbeddingGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();

        // Setup in-memory database
        var options = new DbContextOptionsBuilder<IngestionCacheDbContext>()
            .UseInMemoryDatabase(databaseName: $"CssTestDb_{Guid.NewGuid()}")
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
    public async Task GetNewOrModifiedDocumentsAsync_ShouldReturnNewCssDocuments()
    {
        // Arrange
        var source = new CssDirectorySource(_testDirectory);

        var testFile1 = Path.Combine(_testDirectory, "styles.css");
        var testFile2 = Path.Combine(_testDirectory, "theme.css");

        await File.WriteAllTextAsync(testFile1, ".header { color: red; }");
        await File.WriteAllTextAsync(testFile2, "body { font-size: 16px; }");

        // Act
        var result = await source.GetNewOrModifiedDocumentsAsync(_dbContext.Documents.AsQueryable());

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(result, d => d.Id == "styles.css");
        Assert.Contains(result, d => d.Id == "theme.css");
    }

    [Fact]
    public async Task GetDeletedDocumentsAsync_ShouldReturnDeletedCssDocuments()
    {
        // Arrange
        var source = new CssDirectorySource(_testDirectory);

        // Add document to db that doesn't exist in the directory
        _dbContext.Documents.Add(new IngestedDocument
        {
            Id = "deleted.css",
            SourceId = source.SourceId,
            Version = DateTime.UtcNow.ToString("o")
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await source.GetDeletedDocumentsAsync(_dbContext.Documents.AsQueryable());

        // Assert
        Assert.Single(result);
        Assert.Equal("deleted.css", result.First().Id);
    }

    [Fact]
    public async Task CreateRecordsForDocumentAsync_ShouldCreateCssRuleRecords()
    {
        // Arrange
        var source = new CssDirectorySource(_testDirectory);

        var cssContent = @"
            .header {
                color: red;
                font-size: 18px;
            }
            
            nav.menu {
                background-color: #333;
                padding: 10px;
            }
        ";

        var testFile = Path.Combine(_testDirectory, "test.css");
        await File.WriteAllTextAsync(testFile, cssContent);

        // Setup mock embedding generator
        var testEmbeddings = new[]
        {
            new Embedding<float>(vector),
            new Embedding<float>(vector2)
        };

        var generatedEmbeddings = new GeneratedEmbeddings<Embedding<float>>(testEmbeddings);
        _mockEmbeddingGenerator.Setup(m => m.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedEmbeddings);

        // Act
        var records = await source.CreateRecordsForDocumentAsync(_mockEmbeddingGenerator.Object, "test.css");

        // Assert
        Assert.Equal(2, records.Count()); // Should extract 2 CSS rules

        var firstRecord = records.First();
        Assert.Equal("test.css", firstRecord.FileName);
        Assert.Equal(1, firstRecord.PageNumber);
        Assert.Contains("header", firstRecord.Text, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains("color: red", firstRecord.Text, StringComparison.InvariantCultureIgnoreCase);

        var secondRecord = records.Skip(1).First();
        Assert.Equal("test.css", secondRecord.FileName);
        Assert.Contains("nav.menu", secondRecord.Text, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains("background-color: #333", secondRecord.Text, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void SourceId_ShouldBeUnique()
    {
        // Arrange
        var source1 = new CssDirectorySource(_testDirectory);
        var source2 = new CssDirectorySource(Path.Combine(_testDirectory, "subfolder"));

        // Act
        var sourceId1 = source1.SourceId;
        var sourceId2 = source2.SourceId;

        // Assert
        Assert.NotEqual(sourceId1, sourceId2);
        Assert.Contains("CssDirectorySource", sourceId1, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains(_testDirectory, sourceId1, StringComparison.InvariantCultureIgnoreCase);
    }
}
