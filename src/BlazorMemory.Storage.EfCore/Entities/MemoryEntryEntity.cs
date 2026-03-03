using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorMemory.Storage.EfCore.Entities;

/// <summary>
/// Database entity that maps to the "Memories" table.
/// Kept separate from the core MemoryEntry record to avoid
/// coupling the domain model to EF Core infrastructure concerns.
/// </summary>
[Table("Memories")]
public sealed class MemoryEntryEntity
{
    [Key]
    [MaxLength(64)]
    public required string Id { get; set; }

    [Required]
    [MaxLength(256)]
    public required string UserId { get; set; }

    [Required]
    public required string Content { get; set; }

    /// <summary>
    /// Embedding stored as a JSON-serialized float array.
    /// For PostgreSQL with pgvector, override this with a vector column.
    /// </summary>
    [Required]
    public required string EmbeddingJson { get; set; }

    public required DateTimeOffset LearnedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Metadata stored as a JSON object string.</summary>
    public string MetadataJson { get; set; } = "{}";
}
