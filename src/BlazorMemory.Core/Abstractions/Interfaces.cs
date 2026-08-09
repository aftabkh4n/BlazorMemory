using BlazorMemory.Core.Models;

namespace BlazorMemory.Core.Abstractions;

public interface IMemoryService
{
    Task ExtractAsync(string conversation, string userId, string? @namespace = null, CancellationToken ct = default);

    Task<IReadOnlyList<MemoryEntry>> QueryAsync(string context, string userId, QueryOptions? options = null, CancellationToken ct = default);

    Task<IReadOnlyList<MemoryEntry>> ListAsync(string userId, string? @namespace = null, CancellationToken ct = default);

    Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default);
    Task UpdateAsync(string id, string content, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);

    Task ClearAsync(string userId, string? @namespace = null, CancellationToken ct = default);

    Task<string> ExportAsync(string userId, string? @namespace = null, CancellationToken ct = default);
    Task ImportAsync(string userId, string json, string? @namespace = null, CancellationToken ct = default);

    Task StoreVerbatimAsync(string userId, string content, Dictionary<string, string>? metadata = null, CancellationToken ct = default);
    Task<IReadOnlyList<VerbatimMemory>> SearchVerbatimAsync(string userId, string query, int limit = 10, CancellationToken ct = default);
    Task DeleteVerbatimAsync(string id, CancellationToken ct = default);
    Task ClearVerbatimAsync(string userId, CancellationToken ct = default);
    Task<string> ExportVerbatimAsync(string userId, CancellationToken ct = default);
    Task ImportVerbatimAsync(string userId, string json, CancellationToken ct = default);

    /// <summary>Marks a memory as important (boosts relevance in search).</summary>
    Task MarkVerbatimImportantAsync(string memoryId, CancellationToken ct = default);
    Task MarkVerbatimUnimportantAsync(string memoryId, CancellationToken ct = default);
    Task ResetVerbatimImportanceAsync(string memoryId, CancellationToken ct = default);

    Task MarkImportantAsync(string memoryId, CancellationToken ct = default);

    /// <summary>Marks a memory as unimportant (down-ranks in search, does not delete).</summary>
    Task MarkUnimportantAsync(string memoryId, CancellationToken ct = default);

    /// <summary>Resets importance back to neutral (1.0).</summary>
    Task ResetImportanceAsync(string memoryId, CancellationToken ct = default);

    /// <summary>Sets a custom importance score (typical range 0.0 to 2.0).</summary>
    Task SetImportanceAsync(string memoryId, float score, CancellationToken ct = default);

    /// <summary>
    /// Collapses memories older than the most recent <paramref name="keepRecent"/> into a single
    /// "[Summary]" entry when the total count exceeds <paramref name="maxMemories"/>.
    /// </summary>
    Task SummarizeOldMemoriesAsync(
        string userId,
        int maxMemories = 50,
        int keepRecent = 20,
        string? @namespace = null,
        CancellationToken ct = default);

    /// <summary>
    /// Convenience wrapper: queries relevant memories, injects them into the system prompt,
    /// calls <paramref name="llmCall"/>, extracts new memories, and returns the reply.
    /// </summary>
    Task<string> ChatWithMemoryAsync(
        string userMessage,
        string userId,
        Func<string, string, Task<string>> llmCall,
        QueryOptions? queryOptions = null,
        string? @namespace = null,
        CancellationToken ct = default);
}

public interface IMemoryStore
{
    Task<string> AddAsync(MemoryEntry entry, CancellationToken ct = default);
    Task UpdateAsync(MemoryEntry entry, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default);

    Task<IReadOnlyList<MemoryEntry>> ListAsync(string userId, string? @namespace = null, CancellationToken ct = default);

    Task<IReadOnlyList<MemoryEntry>> SearchSimilarAsync(
        float[] queryEmbedding, string userId, int limit, float threshold,
        string? @namespace = null, CancellationToken ct = default);

    Task ClearAsync(string userId, string? @namespace = null, CancellationToken ct = default);
}

public interface IEmbeddingsProvider
{
    int Dimensions { get; }
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken ct = default);
}

public interface IMemoryExtractor
{
    Task<IReadOnlyList<string>> ExtractFactsAsync(string conversation, CancellationToken ct = default);
    Task<ConsolidationDecision> ConsolidateAsync(string newFact, IReadOnlyList<MemoryEntry> similarMemories, CancellationToken ct = default);
    Task<string> SummarizeAsync(IReadOnlyList<MemoryEntry> memories, CancellationToken ct = default);
}

public interface IVerbatimStore
{
    Task StoreAsync(VerbatimMemory memory, CancellationToken ct = default);
    Task<IReadOnlyList<VerbatimMemory>> SearchAsync(string userId, string query, int limit = 10, CancellationToken ct = default);
    Task<IReadOnlyList<VerbatimMemory>> GetRecentAsync(string userId, int limit = 20, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task ClearAsync(string userId, CancellationToken ct = default);
    Task UpdateImportanceAsync(string id, float score, CancellationToken ct = default);
}