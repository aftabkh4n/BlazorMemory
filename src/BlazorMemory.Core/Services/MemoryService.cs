using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Engine;
using BlazorMemory.Core.Models;
using Microsoft.Extensions.Logging;

namespace BlazorMemory.Core.Services;

public sealed class MemoryService : IMemoryService
{
    private readonly IMemoryStore          _store;
    private readonly IEmbeddingsProvider   _embeddings;
    private readonly ExtractionEngine      _engine;
    private readonly ILogger<MemoryService> _logger;

    public MemoryService(
        IMemoryStore store,
        IEmbeddingsProvider embeddings,
        ExtractionEngine engine,
        ILogger<MemoryService> logger)
    {
        _store      = store;
        _embeddings = embeddings;
        _engine     = engine;
        _logger     = logger;
    }

    public Task ExtractAsync(
        string conversation,
        string userId,
        string? @namespace = null,
        CancellationToken ct = default)
        => _engine.RunAsync(conversation, userId, @namespace, ct);

    public async Task<IReadOnlyList<MemoryEntry>> QueryAsync(
        string context,
        string userId,
        QueryOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new QueryOptions();
        var embedding = await _embeddings.EmbedAsync(context, ct);

        var results = await _store.SearchSimilarAsync(
            embedding, userId,
            options.Limit,
            options.Threshold,
            options.Namespace,
            ct);

        // Filter by age if requested
        if (options.MaxAgeInDays.HasValue)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-options.MaxAgeInDays.Value);
            results = results.Where(m => m.LearnedAt >= cutoff).ToList();
        }

        // Attach staleness scores if requested
        if (options.IncludeStalenessScore)
        {
            results = results
                .Select(m => m.WithStalenessScore(
                    StalenessCalculator.Calculate(m, options.StalenessHalfLifeDays)))
                .ToList();
        }

        return results;
    }

    public Task<IReadOnlyList<MemoryEntry>> ListAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default)
        => _store.ListAsync(userId, @namespace, ct);

    public Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default)
        => _store.GetAsync(id, ct);

    public async Task UpdateAsync(string id, string content, CancellationToken ct = default)
    {
        var existing = await _store.GetAsync(id, ct);
        if (existing is null) return;

        var embedding = await _embeddings.EmbedAsync(content, ct);
        await _store.UpdateAsync(existing with
        {
            Content   = content,
            Embedding = embedding,
            UpdatedAt = DateTimeOffset.UtcNow
        }, ct);
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
        => _store.DeleteAsync(id, ct);

    public Task ClearAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default)
        => _store.ClearAsync(userId, @namespace, ct);
}