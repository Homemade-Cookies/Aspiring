using System;
using Aspire.ChatApp.Services.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.VectorData;
using Moq;

namespace Aspiring.Tests.Ingestion;

public class IngestionSourceFactoryTests
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly IngestionCacheDbContext _dbContext;

    public IngestionSourceFactoryTests()
    {
        Directory.CreateDirectory(_testDirectory);

        // Setup in-memory database
        var options = new DbContextOptionsBuilder<IngestionCacheDbContext>()
            .UseInMemoryDatabase(databaseName: $"CodeTestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new IngestionCacheDbContext(options);
    }

    [Fact]
    public void CreatePdfSource_ShouldCreatePDFSource()
    {
        // Act
        var source = IngestionSourceFactory.CreatePdfSource(_testDirectory);

        // Assert
        Assert.IsType<PDFDirectorySource>(source);
        Assert.Contains("PDFDirectorySource", source.SourceId, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains(_testDirectory, source.SourceId, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void CreateCssSource_ShouldCreateCssSource()
    {
        // Act
        var source = IngestionSourceFactory.CreateCssSource(_testDirectory);

        // Assert
        Assert.IsType<CssDirectorySource>(source);
        Assert.Contains("CssDirectorySource", source.SourceId, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void CreateCodeSource_ShouldCreateCodeSource()
    {
        // Act
        var source = IngestionSourceFactory.CreateCodeSource(_testDirectory);

        // Assert
        Assert.IsType<CodeDirectorySource>(source);
        Assert.Contains("CodeDirectorySource", source.SourceId, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void CreateMarkupSource_ShouldCreateTextSource()
    {
        // Act
        var source = IngestionSourceFactory.CreateMarkupSource(_testDirectory);

        // Assert
        Assert.IsType<TextDirectorySource>(source);
        Assert.Contains("TextDirectorySource", source.SourceId, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains(".html", source.SourceId, StringComparison.InvariantCultureIgnoreCase); // Should contain the markup extensions
    }

    [Fact]
    public void CreateConfigSource_ShouldCreateTextSource()
    {
        // Act
        var source = IngestionSourceFactory.CreateConfigSource(_testDirectory);

        // Assert
        Assert.IsType<TextDirectorySource>(source);
        Assert.Contains("TextDirectorySource", source.SourceId, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains(".json", source.SourceId, StringComparison.InvariantCultureIgnoreCase); // Should contain the config extensions
    }

    [Fact]
    public void CreateDataSource_ShouldCreateTextSource()
    {
        // Act
        var source = IngestionSourceFactory.CreateDataSource(_testDirectory);

        // Assert
        Assert.IsType<TextDirectorySource>(source);
        Assert.Contains("TextDirectorySource", source.SourceId, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains(".sql", source.SourceId, StringComparison.InvariantCultureIgnoreCase); // Should contain the data extensions
    }

    [Fact]
    public void CreateAllTextSource_ShouldIncludeAllNonCodeTextTypes()
    {
        // Act
        var source = IngestionSourceFactory.CreateAllTextSource(_testDirectory);

        // Assert
        Assert.IsType<TextDirectorySource>(source);
        Assert.Contains(".html", source.SourceId, StringComparison.InvariantCultureIgnoreCase); // markup
        Assert.Contains(".json", source.SourceId, StringComparison.InvariantCultureIgnoreCase); // config
        Assert.Contains(".sql", source.SourceId, StringComparison.InvariantCultureIgnoreCase);  // data
        var split = source.SourceId.Split(':');
        var extensions = split.Last().Split(',');
        Assert.DoesNotContain(".cs", extensions); // Should not contain code extensions
        //Assert.DoesNotContain(".cs", source.SourceId, StringComparison.InvariantCultureIgnoreCase); // Should not contain code extensions
    }

    [Fact]
    public void AddIngestionSources_ShouldRegisterFunctionWithServices()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(config =>
            {
                config.ColorBehavior = LoggerColorBehavior.Enabled;
                config.TimestampFormat = "s";
            }).AddDebug()
            ;
        });

        services.AddSingleton(Mock.Of<IEmbeddingGenerator<string, Embedding<float>>>());
        services.AddSingleton(Mock.Of<IVectorStore>());
        services.AddSingleton(_dbContext);
        services.AddSingleton<DataIngestor>();

        // Act
        services.AddIngestionSources(_testDirectory);
        var provider = services.BuildServiceProvider();

        // Assert
        var ingestTask = provider.GetService<Func<Task>>();
        Assert.NotNull(ingestTask);
    }
}
