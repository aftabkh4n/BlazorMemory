using System.Collections.Concurrent;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Engine;
using BlazorMemory.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorMemory.Storage.InMemory;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IMemoryStore"/>.
/// Data is lost when the process restarts. Ideal for unit tests and rapid prototyping.
/// </summary>
public sealed class InMemoryMemoryStore : IMemoryStore
{
    private readonly ConcurrentDictionary<string, MemoryEntry> _store = new();

    public Task<string> AddAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        _store[entry.Id] = entry;
        return Task.FromResult(entry.Id);
    }

    public Task UpdateAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        _store[entry.Id] = entry;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var entry);
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<MemoryEntry>> ListAsync(string userId, CancellationToken ct = default)
    {
        IReadOnlyList<MemoryEntry> result = _store.Values
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.UpdatedAt ?? e.LearnedAt)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<MemoryEntry>> SearchSimilarAsync(
        float[] queryEmbedding,
        string userId,
        int limit,
        float threshold,
        CancellationToken ct = default)
    {
        IReadOnlyList<MemoryEntry> result = _store.Values
            .Where(e => e.UserId == userId)
            .Select(e => (entry: e, score: VectorMath.CosineSimilarity(queryEmbedding, e.Embedding)))
            .Where(x => x.score >= threshold)
            .OrderByDescending(x => x.score)
            .Take(limit)
            .Select(x => x.entry.WithRelevanceScore(x.score))
            .ToList();

        return Task.FromResult(result);
    }

    public Task ClearAsync(string userId, CancellationToken ct = default)
    {
        var keysToRemove = _store.Values
            .Where(e => e.UserId == userId)
            .Select(e => e.Id)
            .ToList();

        foreach (var key in keysToRemove)
            _store.TryRemove(key, out _);

        return Task.CompletedTask;
    }
}

/// <summary>DI registration extension for InMemoryMemoryStore.</summary>
public static class InMemoryStorageExtensions
{
    /// <summary>
    /// Registers the in-memory storage adapter. Suitable for testing and prototyping only.
    /// </summary>
    public static BlazorMemory.Core.Extensions.BlazorMemoryBuilder UseInMemoryStorage(
        this BlazorMemory.Core.Extensions.BlazorMemoryBuilder builder)
    {
        builder.Services.AddSingleton<IMemoryStore, InMemoryMemoryStore>();
        return builder;
    }
}
