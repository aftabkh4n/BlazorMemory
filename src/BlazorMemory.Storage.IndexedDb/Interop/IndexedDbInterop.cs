using BlazorMemory.Core.Models;
using Microsoft.JSInterop;

namespace BlazorMemory.Storage.IndexedDb.Interop;

/// <summary>
/// Typed C# wrapper around the blazorMemory.js ES module.
/// Handles module loading, lifetime, and maps between C# models and JS DTOs.
/// </summary>
internal sealed class IndexedDbInterop : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    // Path to the JS module as a Blazor static web asset
    private const string ModulePath = "./_content/BlazorMemory.Storage.IndexedDb/js/blazorMemory.js";

    public IndexedDbInterop(IJSRuntime js)
    {
        _moduleTask = new Lazy<Task<IJSObjectReference>>(
            () => js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask());
    }

    private async Task<IJSObjectReference> GetModuleAsync() => await _moduleTask.Value;

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<string> AddAsync(MemoryEntry entry, CancellationToken ct)
    {
        var module = await GetModuleAsync();
        var dto = ToDto(entry);
        return await module.InvokeAsync<string>("addEntry", ct, dto);
    }

    public async Task UpdateAsync(MemoryEntry entry, CancellationToken ct)
    {
        var module = await GetModuleAsync();
        var dto = ToDto(entry);
        await module.InvokeVoidAsync("updateEntry", ct, dto);
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("deleteEntry", ct, id);
    }

    public async Task<MemoryEntry?> GetAsync(string id, CancellationToken ct)
    {
        var module = await GetModuleAsync();
        var dto = await module.InvokeAsync<MemoryEntryDto?>("getEntry", ct, id);
        return dto is null ? null : FromDto(dto);
    }

    public async Task<IReadOnlyList<MemoryEntry>> ListAsync(string userId, CancellationToken ct)
    {
        var module = await GetModuleAsync();
        var dtos = await module.InvokeAsync<List<MemoryEntryDto>>("listEntries", ct, userId);
        return dtos.Select(FromDto).ToList();
    }

    public async Task<IReadOnlyList<MemoryEntry>> SearchSimilarAsync(
        float[] queryEmbedding,
        string userId,
        int limit,
        float threshold,
        CancellationToken ct)
    {
        var module = await GetModuleAsync();
        var dtos = await module.InvokeAsync<List<MemoryEntryDto>>(
            "searchSimilar", ct, queryEmbedding, userId, limit, threshold);

        return dtos.Select(FromDto).ToList();
    }

    public async Task ClearAsync(string userId, CancellationToken ct)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("clearEntries", ct, userId);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static MemoryEntryDto ToDto(MemoryEntry e) => new()
    {
        Id        = e.Id,
        UserId    = e.UserId,
        Content   = e.Content,
        Embedding = e.Embedding,
        LearnedAt = e.LearnedAt.ToString("O"),
        UpdatedAt = e.UpdatedAt?.ToString("O"),
        Metadata  = e.Metadata
    };

    private static MemoryEntry FromDto(MemoryEntryDto d) => new()
    {
        Id             = d.Id,
        UserId         = d.UserId,
        Content        = d.Content,
        Embedding      = d.Embedding,
        LearnedAt      = DateTimeOffset.Parse(d.LearnedAt),
        UpdatedAt      = d.UpdatedAt is null ? null : DateTimeOffset.Parse(d.UpdatedAt),
        Metadata       = d.Metadata,
        RelevanceScore = d.RelevanceScore
    };

    // ── Disposal ──────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}
