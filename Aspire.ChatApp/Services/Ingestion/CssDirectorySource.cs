using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using System.Text.RegularExpressions;

namespace Aspire.ChatApp.Services.Ingestion;

internal sealed class CssDirectorySource : IIngestionSource
{
    private readonly string _sourceDirectory;
    private static readonly Regex SelectorRegex = new(@"([^{]+)\s*{([^}]*)}", RegexOptions.Compiled);

    public CssDirectorySource(string sourceDirectory)
    {
        _sourceDirectory = sourceDirectory;
    }

    public static string SourceFileId(string path) => Path.GetFileName(path);

    public string SourceId => $"{nameof(CssDirectorySource)}:{_sourceDirectory}";

    public async Task<IEnumerable<IngestedDocument>> GetNewOrModifiedDocumentsAsync(IQueryable<IngestedDocument> existingDocuments)
    {
        var results = new List<IngestedDocument>();
        var sourceFiles = Directory.GetFiles(_sourceDirectory, "*.css", SearchOption.AllDirectories);

        foreach (var sourceFile in sourceFiles)
        {
            var sourceFileId = SourceFileId(sourceFile);
            var sourceFileVersion = File.GetLastWriteTimeUtc(sourceFile).ToString("o");

            var existingDocument = await existingDocuments
                .Where(d => d.SourceId == SourceId && d.Id == sourceFileId)
                .FirstOrDefaultAsync();

            if (existingDocument is null)
            {
                results.Add(new() { Id = sourceFileId, Version = sourceFileVersion, SourceId = SourceId });
            }
            else if (existingDocument.Version != sourceFileVersion)
            {
                existingDocument.Version = sourceFileVersion;
                results.Add(existingDocument);
            }
        }

        return results;
    }

    public async Task<IEnumerable<IngestedDocument>> GetDeletedDocumentsAsync(IQueryable<IngestedDocument> existingDocuments)
    {
        var sourceFiles = Directory.GetFiles(_sourceDirectory, "*.css", SearchOption.AllDirectories);
        var sourceFileIds = sourceFiles.Select(SourceFileId).ToList();
        return await existingDocuments
            .Where(d => d.SourceId == SourceId && !sourceFileIds.Contains(d.Id))
            .ToListAsync();
    }

    public async Task<IEnumerable<SemanticSearchRecord>> CreateRecordsForDocumentAsync(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, string documentId)
    {
        var filePath = Path.Combine(_sourceDirectory, documentId);
        if (!File.Exists(filePath))
        {
            return Enumerable.Empty<SemanticSearchRecord>();
        }

        var cssContent = await File.ReadAllTextAsync(filePath);
        var cssRules = ExtractCssRules(cssContent);

        // Generate embeddings for each CSS rule
        var embeddings = await embeddingGenerator.GenerateAsync(cssRules);

        return cssRules.Zip(embeddings).Select((pair, index) => new SemanticSearchRecord
        {
            Key = $"{Path.GetFileNameWithoutExtension(documentId)}_css_{index}",
            FileName = documentId,
            PageNumber = 1, // CSS files don't have pages
            Text = pair.First,
            Vector = pair.Second.Vector,
        });
    }

    /// <summary>
    /// Extract CSS rules from content with meaningful context
    /// </summary>
    private static List<string> ExtractCssRules(string cssContent)
    {
        var rules = new List<string>();
        var matches = SelectorRegex.Matches(cssContent);

        foreach (Match match in matches)
        {
            if (match.Success && match.Groups.Count >= 3)
            {
                var selector = match.Groups[1].Value.Trim();
                var properties = match.Groups[2].Value.Trim();

                // Format as readable text for better semantic search
                var ruleText = $"CSS Selector: {selector}\nProperties: {properties}";
                rules.Add(ruleText);
            }
        }

        return rules;
    }
}
