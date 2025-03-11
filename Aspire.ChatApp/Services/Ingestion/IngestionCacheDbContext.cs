using Microsoft.EntityFrameworkCore;

namespace Aspire.ChatApp.Services.Ingestion;

// A DbContext that keeps track of which documents have been ingested.
// This makes it possible to avoid re-ingesting documents that have not changed,
// and to delete documents that have been removed from the underlying source.
internal sealed class IngestionCacheDbContext(DbContextOptions<IngestionCacheDbContext> options) : DbContext(options)
{
    public DbSet<IngestedDocument> Documents { get; set; } = default!;
    public DbSet<IngestedRecord> Records { get; set; } = default!;

    public static void Initialize(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        using var db = scope.ServiceProvider.GetRequiredService<IngestionCacheDbContext>();
        db.Database.EnsureCreated();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<IngestedDocument>().HasMany(d => d.Records).WithOne().HasForeignKey(r => r.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class IngestedDocument
{
    // TODO: Make Id+SourceId a composite key
    public required string Id { get; set; }
    public required string SourceId { get; set; }
    public required string Version { get; set; }
    public List<IngestedRecord> Records { get; set; } = [];
}

internal sealed class IngestedRecord
{
    public required string Id { get; set; }
    public required string DocumentId { get; set; }
}
