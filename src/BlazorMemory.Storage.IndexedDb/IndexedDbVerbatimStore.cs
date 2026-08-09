using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using Microsoft.JSInterop;

namespace BlazorMemory.Storage.IndexedDb;

public class IndexedDbVerbatimStore : IVerbatimStore
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    private const string ModulePath = "./_content/BlazorMemory.Storage.IndexedDb/js/blazorMemory.js";

    public IndexedDbVerbatimStore(IJSRuntime js)
    {
        _moduleTask = new Lazy<Task<IJSObjectReference>>(
            () => js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask());
    }

    public async Task StoreAsync(VerbatimMemory memory, CancellationToken ct = default)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("storeVerbatimMemory", ct, memory);
    }

    public async Task<IReadOnlyList<VerbatimMemory>> SearchAsync(
        string userId,
        string query,
        int limit = 10,
        CancellationToken ct = default)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<List<VerbatimMemory>>(
            "searchVerbatimMemory",
            ct,
            userId,
            query,
            limit);
    }

    public async Task<IReadOnlyList<VerbatimMemory>> GetRecentAsync(
        string userId,
        int limit = 20,
        CancellationToken ct = default)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<List<VerbatimMemory>>(
            "getRecentVerbatimMemory",
            ct,
            userId,
            limit);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("deleteVerbatimMemory", ct, id);
    }

    public async Task ClearAsync(string userId, CancellationToken ct = default)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("clearVerbatimMemory", ct, userId);
    }

    public async Task UpdateImportanceAsync(string id, float score, CancellationToken ct = default)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("updateVerbatimImportance", ct, id, score);
    }

    private async Task<IJSObjectReference> GetModuleAsync() => await _moduleTask.Value;
}
