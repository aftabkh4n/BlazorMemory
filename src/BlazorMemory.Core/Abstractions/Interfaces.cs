using BlazorMemory.Core.Models;

namespace BlazorMemory.Core.Abstractions;

/// <summary>
/// Primary entry point for BlazorMemory. Inject this into your Blazor components or services.
/// </summary>
public interface IMemoryService
{
    /// <summary>
    /// Extracts facts from a conversation and consolidates them with existing memories.
    /// Uses a two-step LLM process: extract facts → for each fact: ADD / UPDATE / DELETE / NONE.
    /// </summary>
    Task ExtractAsync(string conversation, string userId, CancellationToken ct = default);

    /// <summary>
    /// Finds memories semantically relevant to the given context string.
    /// </summary>
    Task<IReadOnlyList<MemoryEntry>> QueryAsync(
        string context,
        string userId,
        QueryOptions? options = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<MemoryEntry>> ListAsync(string userId, CancellationToken ct = default);
    Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default);
    Task UpdateAsync(string id, string content, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task ClearAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// Pluggable storage backend. Implement this to add IndexedDB, EF Core, localStorage, etc.
/// </summary>
public interface IMemoryStore
{
    Task<string> AddAsync(MemoryEntry entry, CancellationToken ct = default);
    Task UpdateAsync(MemoryEntry entry, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<MemoryEntry>> ListAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Returns up to <paramref name="limit"/> memories with cosine similarity above
    /// <paramref name="threshold"/> against the query embedding, sorted by similarity descending.
    /// </summary>
    Task<IReadOnlyList<MemoryEntry>> SearchSimilarAsync(
        float[] queryEmbedding,
        string userId,
        int limit,
        float threshold,
        CancellationToken ct = default);

    Task ClearAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// Pluggable embeddings provider. Converts text into float vectors for similarity search.
/// </summary>
public interface IEmbeddingsProvider
{
    /// <summary>Dimensionality of the embedding vectors this provider produces.</summary>
    int Dimensions { get; }

    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken ct = default);
}

/// <summary>
/// Pluggable LLM-based extractor. Pulls facts from conversations and makes consolidation decisions.
/// </summary>
public interface IMemoryExtractor
{
    /// <summary>Extracts discrete, self-contained facts from the conversation text.</summary>
    Task<IReadOnlyList<string>> ExtractFactsAsync(string conversation, CancellationToken ct = default);

    /// <summary>
    /// Given a newly extracted fact and the most similar existing memories, decides what to do.
    /// </summary>
    Task<ConsolidationDecision> ConsolidateAsync(
        string newFact,
        IReadOnlyList<MemoryEntry> similarMemories,
        CancellationToken ct = default);
}
