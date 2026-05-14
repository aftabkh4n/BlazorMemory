using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using System.Collections.Concurrent;

namespace BlazorMemory.Storage.InMemory;

public class InMemoryVerbatimStore : IVerbatimStore
{
    private readonly ConcurrentDictionary<string, List<VerbatimMemory>> _store = new();

    public Task StoreAsync(VerbatimMemory memory)
    {
        var list = _store.GetOrAdd(memory.UserId, _ => new List<VerbatimMemory>());
        list.Add(memory);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VerbatimMemory>> SearchAsync(
        string userId,
        string query,
        int limit = 10)
    {
        if (!_store.TryGetValue(userId, out var list))
            return Task.FromResult<IReadOnlyList<VerbatimMemory>>(Array.Empty<VerbatimMemory>());

        var results = list
            .Where(x => x.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<VerbatimMemory>>(results);
    }

    public Task<IReadOnlyList<VerbatimMemory>> GetRecentAsync(string userId, int limit = 20)
    {
        if (!_store.TryGetValue(userId, out var list))
            return Task.FromResult<IReadOnlyList<VerbatimMemory>>(Array.Empty<VerbatimMemory>());

        return Task.FromResult<IReadOnlyList<VerbatimMemory>>(
            list.OrderByDescending(x => x.CreatedAt).Take(limit).ToList());
    }

    public Task DeleteAsync(string id)
    {
        foreach (var kvp in _store)
        {
            var item = kvp.Value.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                kvp.Value.Remove(item);
                break;
            }
        }

        return Task.CompletedTask;
    }
}