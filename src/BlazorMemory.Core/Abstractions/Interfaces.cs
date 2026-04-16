using BlazorMemory.Core.Models;

namespace BlazorMemory.Core.Abstractions;

/// <summary>
/// Primary entry point for BlazorMemory. Inject this into your Blazor components or services.
/// </summary>
public interface IMemoryService
{
    /// <summary>
    /// Extracts facts from a conversation and consolidates them with existing memories.
    /// </summary>
    /// <param name="namespace">Optional namespace to scope memories (e.g. "work", "personal").</param>
    Task ExtractAsync(
        string conversation,
        string userId,
        string? @namespace = null,
        CancellationToken ct = default);

    /// <summary>
    /// Finds memories semantically relevant to the given context string.
    /// </summary>
    Task<IReadOnlyList<MemoryEntry>> QueryAsync(
        string context,
        string userId,
        QueryOptions? options = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<MemoryEntry>> ListAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default);

    Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default);
    Task UpdateAsync(string id, string content, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);

    Task ClearAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default);
}

/// <summary>
/// Pluggable storage backend.
/// </summary>
public interface IMemoryStore
{
    Task<string> AddAsync(MemoryEntry entry, CancellationToken ct = default);
    Task UpdateAsync(MemoryEntry entry, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default);

    Task<IReadOnlyList<MemoryEntry>> ListAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<MemoryEntry>> SearchSimilarAsync(
        float[] queryEmbedding,
        string userId,
        int limit,
        float threshold,
        string? @namespace = null,
        CancellationToken ct = default);

    Task ClearAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default);
}

/// <summary>
/// Pluggable embeddings provider.
/// </summary>
public interface IEmbeddingsProvider
{
    int Dimensions { get; }
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken ct = default);
}

/// <summary>
/// Pluggable LLM-based extractor.
/// </summary>
public interface IMemoryExtractor
{
    Task<IReadOnlyList<string>> ExtractFactsAsync(string conversation, CancellationToken ct = default);
    Task<ConsolidationDecision> ConsolidateAsync(
        string newFact,
        IReadOnlyList<MemoryEntry> similarMemories,
        CancellationToken ct = default);
}