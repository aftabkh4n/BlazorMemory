using BlazorMemory.Core.Models;

namespace BlazorMemory.Core.Abstractions;

public interface IVerbatimStore
{
    Task StoreAsync(VerbatimMemory memory);

    Task<IReadOnlyList<VerbatimMemory>> SearchAsync(
        string userId,
        string query,
        int limit = 10);

    Task<IReadOnlyList<VerbatimMemory>> GetRecentAsync(
        string userId,
        int limit = 20);

    Task DeleteAsync(string id);
}