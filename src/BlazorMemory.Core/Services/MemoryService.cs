using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Engine;
using BlazorMemory.Core.Models;
using Microsoft.Extensions.Logging;

namespace BlazorMemory.Core.Services;

/// <summary>
/// Default implementation of <see cref="IMemoryService"/>.
/// Registered automatically by AddBlazorMemory().
/// </summary>
public sealed class MemoryService : IMemoryService
{
    private readonly IMemoryStore _store;
    private readonly IEmbeddingsProvider _embeddings;
    private readonly ExtractionEngine _extractionEngine;
    private readonly ILogger<MemoryService> _logger;

    public MemoryService(
        IMemoryStore store,
        IEmbeddingsProvider embeddings,
        ExtractionEngine extractionEngine,
        ILogger<MemoryService> logger)
    {
        _store = store;
        _embeddings = embeddings;
        _extractionEngine = extractionEngine;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ExtractAsync(string conversation, string userId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversation);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        _logger.LogInformation("Starting memory extraction for user {UserId}", userId);
        await _extractionEngine.RunAsync(conversation, userId, ct);
        _logger.LogInformation("Memory extraction complete for user {UserId}", userId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MemoryEntry>> QueryAsync(
        string context,
        string userId,
        QueryOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        options ??= new QueryOptions();

        var queryEmbedding = await _embeddings.EmbedAsync(context, ct);

        var results = await _store.SearchSimilarAsync(
            queryEmbedding,
            userId,
            options.Limit,
            options.Threshold,
            ct);

        // Filter by max age if requested
        if (options.MaxAgeInDays.HasValue)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-options.MaxAgeInDays.Value);
            results = results
                .Where(m => (m.UpdatedAt ?? m.LearnedAt) >= cutoff)
                .ToList();
        }

        // Apply staleness scores if requested
        if (options.IncludeStalenessScore)
        {
            results = StalenessCalculator.ApplyTo(results, options.StalenessHalfLifeDays);
        }

        return results;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MemoryEntry>> ListAsync(string userId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return _store.ListAsync(userId, ct);
    }

    /// <inheritdoc />
    public Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _store.GetAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(string id, string content, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var entry = await _store.GetAsync(id, ct)
            ?? throw new KeyNotFoundException($"Memory with id '{id}' not found.");

        var newEmbedding = await _embeddings.EmbedAsync(content, ct);
        var updated = entry with
        {
            Content = content,
            Embedding = newEmbedding,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _store.UpdateAsync(updated, ct);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _store.DeleteAsync(id, ct);
    }

    /// <inheritdoc />
    public Task ClearAsync(string userId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return _store.ClearAsync(userId, ct);
    }
}
