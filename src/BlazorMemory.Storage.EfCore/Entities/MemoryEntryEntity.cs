using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorMemory.Storage.EfCore.Entities;

public sealed class MemoryEntryEntity
{
    [Key]
    public required string Id        { get; set; }
    public required string UserId    { get; set; }
    public required string Content   { get; set; }
    public required DateTimeOffset LearnedAt { get; set; }

    /// <summary>Optional namespace for segmenting memories.</summary>
    public string? Namespace { get; set; }

    public DateTimeOffset? UpdatedAt    { get; set; }
    public required string EmbeddingJson { get; set; }
    public string?         MetadataJson  { get; set; }
}