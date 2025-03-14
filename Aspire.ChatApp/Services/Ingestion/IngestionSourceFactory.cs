namespace Aspire.ChatApp.Services.Ingestion;

internal static class IngestionSourceFactory
{
    // Common file extensions grouped by category
    private static readonly string[] CodeFileExtensions = [".cs", ".ts", ".tsx", ".js", ".jsx"];
    private static readonly string[] MarkupFileExtensions = [".xaml", ".html", ".md"];
    private static readonly string[] ConfigFileExtensions = [".json", ".csproj", ".sln"];
    private static readonly string[] DataFileExtensions = [".sql"];
    // CSS has specialized handling, so we don't include it in the text extensions

    /// <summary>
    /// Creates a PDF ingestion source for the specified directory
    /// </summary>
    internal static IIngestionSource CreatePdfSource(string directory)
    {
        return new PDFDirectorySource(directory);
    }

    /// <summary>
    /// Creates a CSS ingestion source with specialized CSS rule parsing
    /// </summary>
    internal static IIngestionSource CreateCssSource(string directory)
    {
        return new CssDirectorySource(directory);
    }

    /// <summary>
    /// Creates a specialized code file ingestion source (C#, TypeScript, JavaScript) 
    /// that extracts classes, methods, and other code structures
    /// </summary>
    internal static IIngestionSource CreateCodeSource(string directory)
    {
        return new CodeDirectorySource(directory, CodeFileExtensions);
    }

    /// <summary>
    /// Creates a markup file ingestion source (HTML, Markdown, XAML) for the specified directory
    /// </summary>
    internal static IIngestionSource CreateMarkupSource(string directory)
    {
        return new TextDirectorySource(directory, MarkupFileExtensions);
    }

    /// <summary>
    /// Creates a configuration file ingestion source (JSON, project files) for the specified directory
    /// </summary>
    internal static IIngestionSource CreateConfigSource(string directory)
    {
        return new TextDirectorySource(directory, ConfigFileExtensions);
    }

    /// <summary>
    /// Creates a data file ingestion source (SQL) for the specified directory
    /// </summary>
    internal static IIngestionSource CreateDataSource(string directory)
    {
        return new TextDirectorySource(directory, DataFileExtensions);
    }

    /// <summary>
    /// Creates a combined ingestion source for all non-code text file types
    /// </summary>
    internal static IIngestionSource CreateAllTextSource(string directory)
    {
        var allNonCodeTextExtensions = MarkupFileExtensions
            .Concat(ConfigFileExtensions)
            .Concat(DataFileExtensions)
            .ToArray();

        return new TextDirectorySource(directory, allNonCodeTextExtensions);
    }

    /// <summary>
    /// Registers ingestion sources with the service collection
    /// </summary>
    internal static IServiceCollection AddIngestionSources(this IServiceCollection services, string baseDirectory, bool includePdf = true, bool includeCss = true, bool includeCode = true)
    {
        // Add the factory as a singleton for convenience
        services.AddSingleton(sp =>
        {
            var ingestor = sp.GetRequiredService<DataIngestor>();

            // Create an async initialization method
            return new Func<Task>(async () =>
            {
                // Ingest PDF files if requested
                if (includePdf)
                {
                    await ingestor.IngestDataAsync(CreatePdfSource(baseDirectory));
                }

                // Ingest CSS files with specialized handling
                if (includeCss)
                {
                    await ingestor.IngestDataAsync(CreateCssSource(baseDirectory));
                }

                // Ingest code files with specialized handling
                if (includeCode)
                {
                    await ingestor.IngestDataAsync(CreateCodeSource(baseDirectory));
                }

                // Ingest all other supported text file types
                await ingestor.IngestDataAsync(CreateAllTextSource(baseDirectory));
            });
        });

        return services;
    }
}