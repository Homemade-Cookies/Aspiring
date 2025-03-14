using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Text;

namespace Aspire.ChatApp.Services.Ingestion;

internal sealed class TextDirectorySource : IIngestionSource
{
    private readonly string _sourceDirectory;
    private readonly string[] _fileExtensions;
    private readonly int _chunkSize;

    public TextDirectorySource(string sourceDirectory, string[] fileExtensions, int chunkSize = 200)
    {
        _sourceDirectory = sourceDirectory;
        _fileExtensions = fileExtensions;
        _chunkSize = chunkSize;
    }

    public static string SourceFileId(string path) => Path.GetFileName(path);

    public string SourceId => $"{nameof(TextDirectorySource)}:{_sourceDirectory}:{string.Join(",", _fileExtensions)}";

    public async Task<IEnumerable<IngestedDocument>> GetNewOrModifiedDocumentsAsync(IQueryable<IngestedDocument> existingDocuments)
    {
        var results = new List<IngestedDocument>();

        foreach (var extension in _fileExtensions)
        {
            var sourceFiles = Directory.GetFiles(_sourceDirectory, $"*{extension}", SearchOption.AllDirectories);

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
        }

        return results;
    }

    public async Task<IEnumerable<IngestedDocument>> GetDeletedDocumentsAsync(IQueryable<IngestedDocument> existingDocuments)
    {
        var allFiles = new List<string>();

        foreach (var extension in _fileExtensions)
        {
            var sourceFiles = Directory.GetFiles(_sourceDirectory, $"*{extension}", SearchOption.AllDirectories);
            allFiles.AddRange(sourceFiles);
        }

        var sourceFileIds = allFiles.Select(SourceFileId).ToList();

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

        var fileContent = await File.ReadAllTextAsync(filePath);
        var relativePath = Path.GetRelativePath(_sourceDirectory, filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        // Split the content into paragraphs for better semantic search
#pragma warning disable SKEXP0050 // Type is for evaluation purposes only
        var chunks = TextChunker.SplitPlainTextParagraphs([fileContent], _chunkSize);
#pragma warning restore SKEXP0050 // Type is for evaluation purposes only

        var embeddings = await embeddingGenerator.GenerateAsync(chunks);

        return chunks.Zip(embeddings).Select((pair, index) => new SemanticSearchRecord
        {
            Key = $"{Path.GetFileNameWithoutExtension(documentId)}_{extension}_{index}",
            FileName = documentId,
            PageNumber = 1, // Text files don't have pages, use 1 as default
            Text = pair.First,
            Vector = pair.Second.Vector,
        });
    }
}