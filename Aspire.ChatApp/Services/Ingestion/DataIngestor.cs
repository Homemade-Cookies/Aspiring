using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace Aspire.ChatApp.Services.Ingestion;

internal sealed class DataIngestor(
    ILogger<DataIngestor> logger,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IVectorStore vectorStore,
    IngestionCacheDbContext ingestionCacheDb)
{
    private static readonly Action<ILogger, string, Exception?> LogRemovingIngestedData =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, "RemovingIngestedData"),
            "Removing ingested data for {File}");

    private static readonly Action<ILogger, string, Exception?> LogProcessingFile =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, "ProcessingFile"),
            "Processing {File}");

    private static readonly Action<ILogger, Exception?> LogIngestionUpToDate =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3, "IngestionUpToDate"),
            "Ingestion is up-to-date");

    public static async Task IngestDataAsync(IServiceProvider services, IIngestionSource source)
    {
        using var scope = services.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<DataIngestor>();
        await ingestor.IngestDataAsync(source);
    }

    public async Task IngestDataAsync(IIngestionSource source)
    {
        var vectorCollection = vectorStore.GetCollection<string, SemanticSearchRecord>("data-aspire_chatapp-ingested");
        await vectorCollection.CreateCollectionIfNotExistsAsync();

        var documentsForSource = ingestionCacheDb.Documents
            .Where(d => d.SourceId == source.SourceId)
            .Include(d => d.Records);

        var deletedFiles = await source.GetDeletedDocumentsAsync(documentsForSource);
        foreach (var deletedFile in deletedFiles)
        {
            LogRemovingIngestedData(logger, deletedFile.Id, null);
            await vectorCollection.DeleteBatchAsync(deletedFile.Records.Select(r => r.Id));
            ingestionCacheDb.Documents.Remove(deletedFile);
        }
        await ingestionCacheDb.SaveChangesAsync();

        var modifiedDocs = await source.GetNewOrModifiedDocumentsAsync(documentsForSource);
        foreach (var modifiedDoc in modifiedDocs)
        {
            LogProcessingFile(logger, modifiedDoc.Id, null);

            if (modifiedDoc.Records.Count > 0)
            {
                await vectorCollection.DeleteBatchAsync(modifiedDoc.Records.Select(r => r.Id));
            }

            var newRecords = await source.CreateRecordsForDocumentAsync(embeddingGenerator, modifiedDoc.Id);
            await foreach (var id in vectorCollection.UpsertBatchAsync(newRecords))
            { }

            modifiedDoc.Records.Clear();
            modifiedDoc.Records.AddRange(newRecords.Select(r => new IngestedRecord { Id = r.Key, DocumentId = modifiedDoc.Id }));

            if (ingestionCacheDb.Entry(modifiedDoc).State == EntityState.Detached)
            {
                ingestionCacheDb.Documents.Add(modifiedDoc);
            }
        }

        await ingestionCacheDb.SaveChangesAsync();
        LogIngestionUpToDate(logger, null);
    }
}
