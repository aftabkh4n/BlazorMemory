using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using Microsoft.JSInterop;

namespace BlazorMemory.Storage.IndexedDb;

public class IndexedDbVerbatimStore : IVerbatimStore
{
    private readonly IJSRuntime _js;

    public IndexedDbVerbatimStore(IJSRuntime js)
    {
        _js = js;
    }

    public async Task StoreAsync(VerbatimMemory memory)
    {
        await _js.InvokeVoidAsync("storeVerbatimMemory", memory);
    }

    public async Task<IReadOnlyList<VerbatimMemory>> SearchAsync(
        string userId,
        string query,
        int limit = 10)
    {
        return await _js.InvokeAsync<List<VerbatimMemory>>(
            "searchVerbatimMemory",
            userId,
            query,
            limit);
    }

    public async Task<IReadOnlyList<VerbatimMemory>> GetRecentAsync(string userId, int limit = 20)
    {
        return await SearchAsync(userId, "", limit);
    }

    public async Task DeleteAsync(string id)
    {
        await _js.InvokeVoidAsync("deleteVerbatimMemory", id);
    }
}