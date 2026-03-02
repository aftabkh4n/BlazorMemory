using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Extensions;
using BlazorMemory.Core.Models;
using BlazorMemory.Storage.IndexedDb.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace BlazorMemory.Storage.IndexedDb;

/// <summary>
/// <see cref="IMemoryStore"/> implementation backed by the browser's IndexedDB.
/// Runs entirely client-side in Blazor WebAssembly — no server or database required.
///
/// Vector similarity search is performed in JavaScript using cosine similarity,
/// so no server round-trips are needed for querying either.
///
/// Lifetime: Scoped (one instance per Blazor component scope).
/// </summary>
public sealed class IndexedDbMemoryStore : IMemoryStore, IAsyncDisposable
{
    private readonly IndexedDbInterop _interop;

    public IndexedDbMemoryStore(IJSRuntime js)
    {
        _interop = new IndexedDbInterop(js);
    }

    /// <inheritdoc />
    public Task<string> AddAsync(MemoryEntry entry, CancellationToken ct = default)
        => _interop.AddAsync(entry, ct);

    /// <inheritdoc />
    public Task UpdateAsync(MemoryEntry entry, CancellationToken ct = default)
        => _interop.UpdateAsync(entry, ct);

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken ct = default)
        => _interop.DeleteAsync(id, ct);

    /// <inheritdoc />
    public Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default)
        => _interop.GetAsync(id, ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<MemoryEntry>> ListAsync(string userId, CancellationToken ct = default)
        => _interop.ListAsync(userId, ct);

    /// <inheritdoc />
    /// <remarks>
    /// Cosine similarity is computed in JavaScript against all entries for the user.
    /// For typical personal assistant use cases (hundreds to low thousands of memories),
    /// this is fast enough to run synchronously in the browser without a dedicated vector index.
    /// </remarks>
    public Task<IReadOnlyList<MemoryEntry>> SearchSimilarAsync(
        float[] queryEmbedding,
        string userId,
        int limit,
        float threshold,
        CancellationToken ct = default)
        => _interop.SearchSimilarAsync(queryEmbedding, userId, limit, threshold, ct);

    /// <inheritdoc />
    public Task ClearAsync(string userId, CancellationToken ct = default)
        => _interop.ClearAsync(userId, ct);

    public ValueTask DisposeAsync() => _interop.DisposeAsync();
}

/// <summary>
/// DI registration extension for the IndexedDB storage adapter.
/// </summary>
public static class IndexedDbStorageExtensions
{
    /// <summary>
    /// Registers the IndexedDB storage adapter for Blazor WebAssembly.
    /// Memories are stored in the browser's IndexedDB — no backend required.
    ///
    /// Usage in Program.cs:
    /// <code>
    /// builder.Services
    ///     .AddBlazorMemory()
    ///     .UseIndexedDbStorage()
    ///     .UseOpenAiEmbeddings(apiKey)
    ///     .UseOpenAiExtractor(apiKey);
    /// </code>
    /// </summary>
    public static BlazorMemoryBuilder UseIndexedDbStorage(
        this BlazorMemoryBuilder builder)
    {
        // Scoped so each component tree gets one store instance sharing the JS module
        builder.Services.AddScoped<IMemoryStore, IndexedDbMemoryStore>();
        return builder;
    }
}
