using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Engine;
using BlazorMemory.Core.Extensions;
using BlazorMemory.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorMemory.Storage.InMemory;

public sealed class InMemoryMemoryStore : IMemoryStore
{
    private readonly Dictionary<string, MemoryEntry> _store = [];
    private readonly object _lock = new();

    public Task<string> AddAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        lock (_lock) _store[entry.Id] = entry;
        return Task.FromResult(entry.Id);
    }

    public Task UpdateAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        lock (_lock) _store[entry.Id] = entry;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
    {
        lock (_lock) _store.Remove(id);
        return Task.CompletedTask;
    }

    public Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default)
    {
        MemoryEntry? entry;
        lock (_lock) _store.TryGetValue(id, out entry);
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<MemoryEntry>> ListAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            var results = _store.Values
                .Where(m => m.UserId == userId && MatchesNamespace(m, @namespace))
                .ToList();
            return Task.FromResult<IReadOnlyList<MemoryEntry>>(results);
        }
    }

    public Task<IReadOnlyList<MemoryEntry>> SearchSimilarAsync(
        float[] queryEmbedding,
        string userId,
        int limit,
        float threshold,
        string? @namespace = null,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            var results = _store.Values
                .Where(m => m.UserId == userId && MatchesNamespace(m, @namespace))
                .Select(m => m.WithRelevanceScore(VectorMath.CosineSimilarity(queryEmbedding, m.Embedding)))
                .Where(m => m.RelevanceScore >= threshold)
                .OrderByDescending(m => m.RelevanceScore)
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<MemoryEntry>>(results);
        }
    }

    public Task ClearAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            var toRemove = _store.Values
                .Where(m => m.UserId == userId && MatchesNamespace(m, @namespace))
                .Select(m => m.Id)
                .ToList();
            foreach (var id in toRemove) _store.Remove(id);
        }
        return Task.CompletedTask;
    }

    private static bool MatchesNamespace(MemoryEntry m, string? @namespace)
        => @namespace is null || m.Namespace == @namespace;
}

public static class InMemoryStorageExtensions
{
    public static BlazorMemoryBuilder UseInMemoryStorage(this BlazorMemoryBuilder builder)
    {
        builder.Services.AddScoped<IMemoryStore, InMemoryMemoryStore>();
        return builder;
    }
}