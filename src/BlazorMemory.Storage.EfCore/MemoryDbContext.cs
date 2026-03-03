using BlazorMemory.Storage.EfCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorMemory.Storage.EfCore;

/// <summary>
/// EF Core DbContext for BlazorMemory.
///
/// Usage — add to your existing DbContext instead of using this directly:
/// <code>
/// public class AppDbContext : DbContext
/// {
///     public DbSet&lt;MemoryEntryEntity&gt; Memories { get; set; }
///
///     protected override void OnModelCreating(ModelBuilder modelBuilder)
///     {
///         modelBuilder.ApplyBlazorMemoryConfiguration();
///     }
/// }
/// </code>
///
/// Or use this standalone context for apps that don't have their own DbContext.
/// </summary>
public class MemoryDbContext : DbContext
{
    public MemoryDbContext(DbContextOptions<MemoryDbContext> options) : base(options) { }

    public DbSet<MemoryEntryEntity> Memories => Set<MemoryEntryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyBlazorMemoryConfiguration();
    }
}

/// <summary>
/// EF Core model configuration for BlazorMemory entities.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies BlazorMemory table configuration to the model.
    /// Call this from your own DbContext.OnModelCreating() if you want
    /// to share your existing DbContext rather than use MemoryDbContext.
    /// </summary>
    public static ModelBuilder ApplyBlazorMemoryConfiguration(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MemoryEntryEntity>(entity =>
        {
            entity.ToTable("Memories");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.UserId).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.EmbeddingJson).IsRequired();
            entity.Property(e => e.MetadataJson).HasDefaultValue("{}");

            // Index on UserId for fast per-user queries
            entity.HasIndex(e => e.UserId).HasDatabaseName("IX_Memories_UserId");

            // Composite index for time-ordered user queries
            entity.HasIndex(e => new { e.UserId, e.LearnedAt })
                  .HasDatabaseName("IX_Memories_UserId_LearnedAt");
        });

        return modelBuilder;
    }
}
