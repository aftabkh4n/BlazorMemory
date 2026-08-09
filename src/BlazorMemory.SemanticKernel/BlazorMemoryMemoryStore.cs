using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using BlazorMemory.Core.Models;
using Microsoft.SemanticKernel.Memory;
using BMStore = BlazorMemory.Core.Abstractions.IMemoryStore;

namespace BlazorMemory.SemanticKernel;

/// <summary>
/// Adapts BlazorMemory's <see cref="IMemoryStore"/> to Semantic Kernel's
/// <see cref="Microsoft.SemanticKernel.Memory.IMemoryStore"/>.
/// SK collections map to BlazorMemory namespaces; the <paramref name="userId"/>
/// scopes all operations within the underlying store.
/// </summary>
public sealed class BlazorMemoryMemoryStore : Microsoft.SemanticKernel.Memory.IMemoryStore
{
    private readonly BMStore _store;
    private readonly string _userId;
    private readonly ConcurrentDictionary<string, byte> _collections = new(StringComparer.Ordinal);

    public BlazorMemoryMemoryStore(BMStore store, string userId = "sk")
    {
        _store = store;
        _userId = userId;
    }

    public Task CreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        _collections.TryAdd(collectionName, 0);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> GetCollectionsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var entries = await _store.ListAsync(_userId, null, cancellationToken);
        var fromStore = entries
            .Where(e => e.Namespace is not null)
            .Select(e => e.Namespace!)
            .Distinct(StringComparer.Ordinal);

        foreach (var name in _collections.Keys.Union(fromStore, StringComparer.Ordinal).Distinct(StringComparer.Ordinal))
            yield return name;
    }

    public async Task<bool> DoesCollectionExistAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        if (_collections.ContainsKey(collectionName))
            return true;
        var entries = await _store.ListAsync(_userId, collectionName, cancellationToken);
        return entries.Count > 0;
    }

    public async Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        _collections.TryRemove(collectionName, out _);
        await _store.ClearAsync(_userId, collectionName, cancellationToken);
    }

    public async Task<string> UpsertAsync(string collectionName, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        var entry = ToMemoryEntry(collectionName, record);
        var existing = await _store.GetAsync(entry.Id, cancellationToken);
        if (existing is null)
            await _store.AddAsync(entry, cancellationToken);
        else
            await _store.UpdateAsync(entry, cancellationToken);
        return entry.Id;
    }

    public async IAsyncEnumerable<string> UpsertBatchAsync(
        string collectionName,
        IEnumerable<MemoryRecord> records,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
            yield return await UpsertAsync(collectionName, record, cancellationToken);
    }

    public async Task<MemoryRecord?> GetAsync(
        string collectionName,
        string key,
        bool withEmbedding = false,
        CancellationToken cancellationToken = default)
    {
        var entry = await _store.GetAsync(key, cancellationToken);
        if (entry is null || entry.Namespace != collectionName)
            return null;
        return ToMemoryRecord(entry, withEmbedding);
    }

    public async IAsyncEnumerable<MemoryRecord> GetBatchAsync(
        string collectionName,
        IEnumerable<string> keys,
        bool withEmbedding = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            var record = await GetAsync(collectionName, key, withEmbedding, cancellationToken);
            if (record is not null)
                yield return record;
        }
    }

    public Task RemoveAsync(string collectionName, string key, CancellationToken cancellationToken = default)
        => _store.DeleteAsync(key, cancellationToken);

    public async Task RemoveBatchAsync(string collectionName, IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
            await _store.DeleteAsync(key, cancellationToken);
    }

    public async Task<(MemoryRecord, double)?> GetNearestMatchAsync(
        string collectionName,
        ReadOnlyMemory<float> embedding,
        double minRelevanceScore = 0,
        bool withEmbedding = false,
        CancellationToken cancellationToken = default)
    {
        var results = await _store.SearchSimilarAsync(
            embedding.ToArray(), _userId, 1, (float)minRelevanceScore, collectionName, cancellationToken);
        if (results.Count == 0)
            return null;
        var top = results[0];
        return (ToMemoryRecord(top, withEmbedding), top.RelevanceScore ?? 0d);
    }

    public async IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesAsync(
        string collectionName,
        ReadOnlyMemory<float> embedding,
        int limit,
        double minRelevanceScore = 0,
        bool withEmbedding = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var results = await _store.SearchSimilarAsync(
            embedding.ToArray(), _userId, limit, (float)minRelevanceScore, collectionName, cancellationToken);
        foreach (var entry in results)
            yield return (ToMemoryRecord(entry, withEmbedding), entry.RelevanceScore ?? 0d);
    }

    private MemoryEntry ToMemoryEntry(string collectionName, MemoryRecord record)
    {
        var id = string.IsNullOrEmpty(record.Key) ? record.Metadata.Id : record.Key;
        return new MemoryEntry
        {
            Id = id,
            UserId = _userId,
            Content = record.Metadata.Text,
            Embedding = record.Embedding.ToArray(),
            LearnedAt = record.Timestamp ?? DateTimeOffset.UtcNow,
            Namespace = collectionName,
        };
    }

    private static MemoryRecord ToMemoryRecord(MemoryEntry entry, bool withEmbedding)
    {
        var embedding = withEmbedding
            ? new ReadOnlyMemory<float>(entry.Embedding)
            : ReadOnlyMemory<float>.Empty;

        return MemoryRecord.LocalRecord(
            id: entry.Id,
            text: entry.Content,
            description: null,
            embedding: embedding,
            key: entry.Id,
            timestamp: entry.LearnedAt);
    }
}
