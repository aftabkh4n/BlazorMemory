using BlazorMemory.Storage.Pgvector.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorMemory.Storage.Pgvector;

/// <summary>
/// EF Core DbContext with pgvector support for BlazorMemory.
///
/// Usage — add to your existing DbContext:
/// <code>
/// public class AppDbContext : DbContext
/// {
///     public DbSet&lt;PgvectorMemoryEntry&gt; Memories { get; set; }
///
///     protected override void OnModelCreating(ModelBuilder modelBuilder)
///     {
///         modelBuilder.ApplyPgvectorMemoryConfiguration(dimensions: 1536);
///     }
/// }
/// </code>
/// </summary>
public class PgvectorMemoryDbContext : DbContext
{
    private readonly int _dimensions;

    public PgvectorMemoryDbContext(DbContextOptions<PgvectorMemoryDbContext> options, int dimensions = 1536)
        : base(options)
    {
        _dimensions = dimensions;
    }

    public DbSet<PgvectorMemoryEntry> Memories => Set<PgvectorMemoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyPgvectorMemoryConfiguration(_dimensions);
    }
}

public static class PgvectorModelBuilderExtensions
{
    public static ModelBuilder ApplyPgvectorMemoryConfiguration(
        this ModelBuilder modelBuilder,
        int dimensions = 1536)
    {
        modelBuilder.Entity<PgvectorMemoryEntry>(entity =>
        {
            entity.ToTable("Memories");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.UserId).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.MetadataJson).HasDefaultValue("{}");
            entity.Property(e => e.ImportanceScore).HasDefaultValue(1.0f);

            // Native pgvector column
            entity.Property(e => e.Embedding)
                  .HasColumnType($"vector({dimensions})");

            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("IX_Memories_UserId");

            entity.HasIndex(e => new { e.UserId, e.LearnedAt })
                  .HasDatabaseName("IX_Memories_UserId_LearnedAt");

            // HNSW index for fast approximate nearest neighbour search
            entity.HasIndex(e => e.Embedding)
                  .HasMethod("hnsw")
                  .HasOperators("vector_cosine_ops")
                  .HasDatabaseName("IX_Memories_Embedding_Hnsw");
        });

        return modelBuilder;
    }
}
