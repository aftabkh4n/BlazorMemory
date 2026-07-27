using System.ComponentModel.DataAnnotations;
using Pgvector;

namespace BlazorMemory.Storage.Pgvector.Entities;

public sealed class PgvectorMemoryEntry
{
    [Key]
    public required string Id        { get; set; }
    public required string UserId    { get; set; }
    public required string Content   { get; set; }
    public required Vector Embedding { get; set; }
    public required DateTimeOffset LearnedAt { get; set; }

    public string?  Namespace       { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string?  MetadataJson    { get; set; }
    public float    ImportanceScore { get; set; } = 1.0f;
}
