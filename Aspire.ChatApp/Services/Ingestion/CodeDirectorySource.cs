using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Text;
using System.Text.RegularExpressions;

namespace Aspire.ChatApp.Services.Ingestion;

internal sealed class CodeDirectorySource : IIngestionSource
{
    private readonly string _sourceDirectory;
    private readonly string[] _fileExtensions;
    private readonly int _chunkSize;

    // Regular expressions for code structure extraction
    private static readonly Regex ClassRegex = new(@"(public|private|internal|protected)?\s*(static|abstract|sealed)?\s*class\s+(\w+)", RegexOptions.Compiled);
    private static readonly Regex MethodRegex = new(@"(public|private|internal|protected)?\s*(static|virtual|abstract|override|async)?\s*\w+\s+(\w+)\s*\([^)]*\)", RegexOptions.Compiled);

    public CodeDirectorySource(string sourceDirectory, string[] fileExtensions, int chunkSize = 200)
    {
        _sourceDirectory = sourceDirectory;
        _fileExtensions = fileExtensions;
        _chunkSize = chunkSize;
    }

    public static string SourceFileId(string path) => Path.GetFileName(path);

    public string SourceId => $"{nameof(CodeDirectorySource)}:{_sourceDirectory}:{string.Join(",", _fileExtensions)}";

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
        var codeUnits = ExtractCodeUnits(fileContent, documentId);

        // Generate embeddings for each code unit
        var embeddings = await embeddingGenerator.GenerateAsync(codeUnits.Select(c => c.Text));

        return codeUnits.Zip(embeddings).Select((pair, index) => new SemanticSearchRecord
        {
            Key = $"{Path.GetFileNameWithoutExtension(documentId)}_{pair.First.Type}_{index}",
            FileName = documentId,
            PageNumber = 1, // Code files don't have pages, use 1 as default
            Text = pair.First.Text,
            Vector = pair.Second.Vector,
        });
    }

    /// <summary>
    /// Extract structured code units from the code file
    /// </summary>
    private List<(string Type, string Text)> ExtractCodeUnits(string codeContent, string filename)
    {
        var units = new List<(string Type, string Text)>();
        var extension = Path.GetExtension(filename).ToLowerInvariant();

        // Add file overview
        units.Add(("file", $"Filename: {filename}\nLanguage: {GetLanguageFromExtension(extension)}\nOverview: {codeContent.Substring(0, Math.Min(500, codeContent.Length))}"));

        // Extract classes
        var classMatches = ClassRegex.Matches(codeContent);
        foreach (Match match in classMatches)
        {
            if (match.Success)
            {
                var className = match.Groups[3].Value;
                // Find the class body (simplified approach)
                var startIdx = match.Index;
                var classText = ExtractBlock(codeContent, startIdx);
                units.Add(("class", $"Class: {className}\n{classText}"));
            }
        }

        // Extract methods
        var methodMatches = MethodRegex.Matches(codeContent);
        foreach (Match match in methodMatches)
        {
            if (match.Success)
            {
                var methodName = match.Groups[3].Value;
                // Find the method body (simplified approach)
                var startIdx = match.Index;
                var methodText = ExtractBlock(codeContent, startIdx);
                units.Add(("method", $"Method: {methodName}\n{methodText}"));
            }
        }

        // If we didn't extract any specific units, fallback to generic chunking
        if (units.Count <= 1)
        {
#pragma warning disable SKEXP0050 // Type is for evaluation purposes only
            var chunks = TextChunker.SplitPlainTextParagraphs([codeContent], _chunkSize)
                .Select((chunk, i) => ($"chunk{i}", chunk));
            units.AddRange(chunks);
#pragma warning restore SKEXP0050 // Type is for evaluation purposes only
        }

        return units;
    }

    /// <summary>
    /// Extract a code block starting from a given index
    /// </summary>
    private static string ExtractBlock(string content, int startIndex, int maxLength = 1000)
    {
        int braceCount = 0;
        bool started = false;
        int endIndex = startIndex;

        // Simple method to extract a balanced code block
        for (int i = startIndex; i < content.Length && i < startIndex + maxLength; i++)
        {
            if (content[i] == '{')
            {
                started = true;
                braceCount++;
            }
            else if (content[i] == '}')
            {
                braceCount--;
                if (started && braceCount == 0)
                {
                    endIndex = i + 1;
                    break;
                }
            }
            endIndex = i;
        }

        // Include the method signature in the extracted block
        int methodSignatureStart = content.LastIndexOf('\n', startIndex) + 1;
        string methodSignature = content.Substring(methodSignatureStart, startIndex - methodSignatureStart).Trim();

        return methodSignature + "\n" + content.Substring(startIndex, endIndex - startIndex + 1).Trim();
    }

    /// <summary>
    /// Get language name from file extension
    /// </summary>
    private static string GetLanguageFromExtension(string extension)
    {
        return extension switch
        {
            ".cs" => "C#",
            ".ts" => "TypeScript",
            ".js" => "JavaScript",
            ".tsx" => "TypeScript React",
            ".jsx" => "JavaScript React",
            _ => "Unknown"
        };
    }
}
