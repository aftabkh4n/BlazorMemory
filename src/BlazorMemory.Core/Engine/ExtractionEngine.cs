using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using Microsoft.Extensions.Logging;

namespace BlazorMemory.Core.Engine;

/// <summary>
/// Orchestrates the two-step extract → consolidate pipeline.
/// Called by MemoryService.ExtractAsync().
/// </summary>
public sealed class ExtractionEngine
{
    private readonly IMemoryStore _store;
    private readonly IEmbeddingsProvider _embeddings;
    private readonly IMemoryExtractor _extractor;
    private readonly ILogger<ExtractionEngine> _logger;

    // How many similar memories to fetch per fact during consolidation.
    private const int ConsolidationCandidateLimit = 5;
    private const float ConsolidationSimilarityThreshold = 0.75f;

    public ExtractionEngine(
        IMemoryStore store,
        IEmbeddingsProvider embeddings,
        IMemoryExtractor extractor,
        ILogger<ExtractionEngine> logger)
    {
        _store = store;
        _embeddings = embeddings;
        _extractor = extractor;
        _logger = logger;
    }

    /// <summary>
    /// Runs the full extract → embed → consolidate pipeline for a conversation.
    /// </summary>
    public async Task RunAsync(string conversation, string userId, CancellationToken ct = default)
    {
        // Step 1: Extract facts from the conversation
        _logger.LogDebug("Extracting facts from conversation for user {UserId}", userId);
        var facts = await _extractor.ExtractFactsAsync(conversation, ct);

        if (facts.Count == 0)
        {
            _logger.LogDebug("No facts extracted for user {UserId}", userId);
            return;
        }

        _logger.LogDebug("Extracted {Count} fact(s) for user {UserId}", facts.Count, userId);

        // Step 2: Process each fact through consolidation
        foreach (var fact in facts)
        {
            await ProcessFactAsync(fact, userId, ct);
        }
    }

    private async Task ProcessFactAsync(string fact, string userId, CancellationToken ct)
    {
        // Embed the new fact
        var embedding = await _embeddings.EmbedAsync(fact, ct);

        // Find similar existing memories
        var similar = await _store.SearchSimilarAsync(
            embedding, userId,
            ConsolidationCandidateLimit,
            ConsolidationSimilarityThreshold,
            ct);

        // Ask the LLM to decide what to do
        var decision = await _extractor.ConsolidateAsync(fact, similar, ct);

        _logger.LogDebug(
            "Consolidation decision for fact '{Fact}': {Action} (target: {Target})",
            fact, decision.Action, decision.TargetMemoryId ?? "none");

        switch (decision.Action)
        {
            case ConsolidationAction.Add:
                var newEntry = new MemoryEntry
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UserId = userId,
                    Content = fact,
                    Embedding = embedding,
                    LearnedAt = DateTimeOffset.UtcNow
                };
                await _store.AddAsync(newEntry, ct);
                break;

            case ConsolidationAction.Update when decision.TargetMemoryId is not null:
                var existing = await _store.GetAsync(decision.TargetMemoryId, ct);
                if (existing is null) break;

                var updatedContent = decision.UpdatedContent ?? fact;
                var updatedEmbedding = await _embeddings.EmbedAsync(updatedContent, ct);
                var updated = existing with
                {
                    Content = updatedContent,
                    Embedding = updatedEmbedding,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                await _store.UpdateAsync(updated, ct);
                break;

            case ConsolidationAction.Delete when decision.TargetMemoryId is not null:
                await _store.DeleteAsync(decision.TargetMemoryId, ct);
                break;

            case ConsolidationAction.None:
            default:
                break;
        }
    }
}
