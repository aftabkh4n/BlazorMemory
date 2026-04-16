using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Extensions;
using BlazorMemory.Core.Models;
using BlazorMemory.Storage.IndexedDb.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace BlazorMemory.Storage.IndexedDb;

public sealed class IndexedDbMemoryStore : IMemoryStore, IAsyncDisposable
{
    private readonly IndexedDbInterop _interop;

    public IndexedDbMemoryStore(IJSRuntime js)
    {
        _interop = new IndexedDbInterop(js);
    }

    public Task<string> AddAsync(MemoryEntry entry, CancellationToken ct = default)
        => _interop.AddAsync(entry, ct);

    public Task UpdateAsync(MemoryEntry entry, CancellationToken ct = default)
        => _interop.UpdateAsync(entry, ct);

    public Task DeleteAsync(string id, CancellationToken ct = default)
        => _interop.DeleteAsync(id, ct);

    public Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default)
        => _interop.GetAsync(id, ct);

    public async Task<IReadOnlyList<MemoryEntry>> ListAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default)
    {
        var all = await _interop.ListAsync(userId, ct);
        return @namespace is null
            ? all
            : all.Where(m => m.Namespace == @namespace).ToList();
    }

    public async Task<IReadOnlyList<MemoryEntry>> SearchSimilarAsync(
        float[] queryEmbedding,
        string userId,
        int limit,
        float threshold,
        string? @namespace = null,
        CancellationToken ct = default)
    {
        var results = await _interop.SearchSimilarAsync(queryEmbedding, userId, limit, threshold, ct);
        return @namespace is null
            ? results
            : results.Where(m => m.Namespace == @namespace).ToList();
    }

    public Task ClearAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default)
        => _interop.ClearAsync(userId, ct);

    public ValueTask DisposeAsync() => _interop.DisposeAsync();
}

public static class IndexedDbStorageExtensions
{
    public static BlazorMemoryBuilder UseIndexedDbStorage(this BlazorMemoryBuilder builder)
    {
        builder.Services.AddScoped<IMemoryStore, IndexedDbMemoryStore>();
        return builder;
    }
}