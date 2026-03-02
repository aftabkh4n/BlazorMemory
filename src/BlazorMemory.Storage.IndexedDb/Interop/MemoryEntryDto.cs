using System.Text.Json.Serialization;

namespace BlazorMemory.Storage.IndexedDb.Interop;

/// <summary>
/// JSON-serializable DTO that mirrors the JavaScript memory entry object stored in IndexedDB.
/// We keep a separate DTO to control exactly what gets serialized over JS Interop
/// and to avoid polluting the core MemoryEntry record.
/// </summary>
internal sealed class MemoryEntryDto
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("userId")]
    public required string UserId { get; set; }

    [JsonPropertyName("content")]
    public required string Content { get; set; }

    /// <summary>
    /// Embedding stored as a plain float array — IndexedDB serializes this as a JSON number array.
    /// </summary>
    [JsonPropertyName("embedding")]
    public required float[] Embedding { get; set; }

    [JsonPropertyName("learnedAt")]
    public required string LearnedAt { get; set; }   // ISO 8601

    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; set; }            // ISO 8601, nullable

    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = [];

    /// <summary>
    /// Relevance score attached by the JS searchSimilar() function.
    /// Null when not from a search result.
    /// </summary>
    [JsonPropertyName("relevanceScore")]
    public float? RelevanceScore { get; set; }
}
