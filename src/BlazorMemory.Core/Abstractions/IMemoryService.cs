using BlazorMemory.Core.Models;

namespace BlazorMemory.Core.Abstractions;

public interface IMemoryService
{
    // Existing methods
    Task<List<MemoryEntry>> ListAsync(string userId, string? ns);
    Task DeleteAsync(string id);
    Task ClearAsync(string userId, string? ns);
    Task<string> ExportAsync(string userId, string? ns);
    Task ImportAsync(string userId, string json, string? ns);

    // NEW: Verbatim mode
    Task StoreVerbatimAsync(
        string userId,
        string content,
        Dictionary<string, string>? metadata = null);

    Task<IReadOnlyList<VerbatimMemory>> SearchVerbatimAsync(
        string userId,
        string query,
        int limit = 10);

    Task DeleteVerbatimAsync(string id);
    Task ClearVerbatimAsync(string userId);

    Task ImportVerbatimAsync(string userId, string json);
}