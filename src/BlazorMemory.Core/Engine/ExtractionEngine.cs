using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using Microsoft.Extensions.Logging;

namespace BlazorMemory.Core.Engine;

public sealed class ExtractionEngine
{
    private readonly IMemoryStore           _store;
    private readonly IEmbeddingsProvider    _embeddings;
    private readonly IMemoryExtractor       _extractor;
    private readonly ILogger<ExtractionEngine> _logger;

    private const int   ConsolidationCandidateLimit      = 8;
    private const float ConsolidationSimilarityThreshold = 0.60f;

    public ExtractionEngine(
        IMemoryStore store,
        IEmbeddingsProvider embeddings,
        IMemoryExtractor extractor,
        ILogger<ExtractionEngine> logger)
    {
        _store      = store;
        _embeddings = embeddings;
        _extractor  = extractor;
        _logger     = logger;
    }

    public async Task RunAsync(
        string conversation,
        string userId,
        string? @namespace = null,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Extracting facts for user {UserId} namespace={Namespace}", userId, @namespace ?? "default");
        var facts = await _extractor.ExtractFactsAsync(conversation, ct);

        if (facts.Count == 0)
        {
            _logger.LogDebug("No facts extracted for user {UserId}", userId);
            return;
        }

        _logger.LogDebug("Extracted {Count} fact(s) for user {UserId}", facts.Count, userId);

        foreach (var fact in facts)
            await ProcessFactAsync(fact, userId, @namespace, ct);
    }

    private async Task ProcessFactAsync(
        string fact,
        string userId,
        string? @namespace,
        CancellationToken ct)
    {
        var embedding = await _embeddings.EmbedAsync(fact, ct);

        var similar = await _store.SearchSimilarAsync(
            embedding, userId,
            ConsolidationCandidateLimit,
            ConsolidationSimilarityThreshold,
            @namespace,
            ct);

        var decision = await _extractor.ConsolidateAsync(fact, similar, ct);

        _logger.LogDebug(
            "Consolidation for '{Fact}': {Action} (target: {Target})",
            fact, decision.Action, decision.TargetMemoryId ?? "none");

        switch (decision.Action)
        {
            case ConsolidationAction.Add:
                await _store.AddAsync(new MemoryEntry
                {
                    Id        = Guid.NewGuid().ToString("N"),
                    UserId    = userId,
                    Namespace = @namespace,
                    Content   = fact,
                    Embedding = embedding,
                    LearnedAt = DateTimeOffset.UtcNow
                }, ct);
                break;

            case ConsolidationAction.Update when decision.TargetMemoryId is not null:
                var existing = await _store.GetAsync(decision.TargetMemoryId, ct);
                if (existing is null) break;

                var updatedContent   = decision.UpdatedContent ?? fact;
                var updatedEmbedding = await _embeddings.EmbedAsync(updatedContent, ct);
                await _store.UpdateAsync(existing with
                {
                    Content   = updatedContent,
                    Embedding = updatedEmbedding,
                    UpdatedAt = DateTimeOffset.UtcNow
                }, ct);
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