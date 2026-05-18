using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using System.Collections.Concurrent;

namespace BlazorMemory.Storage.InMemory;

public class InMemoryVerbatimStore : IVerbatimStore
{
    private readonly ConcurrentDictionary<string, List<VerbatimMemory>> _store = new();
    private readonly object _lock = new();

    public Task StoreAsync(VerbatimMemory memory, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var list = _store.GetOrAdd(memory.UserId, _ => []);
            list.RemoveAll(x => x.Id == memory.Id);
            list.Add(memory);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VerbatimMemory>> SearchAsync(
        string userId,
        string query,
        int limit = 10,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_store.TryGetValue(userId, out var list))
                return Task.FromResult<IReadOnlyList<VerbatimMemory>>([]);

            var results = list
                .Where(x => x.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.CreatedAt)
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<VerbatimMemory>>(results);
        }
    }

    public Task<IReadOnlyList<VerbatimMemory>> GetRecentAsync(
        string userId,
        int limit = 20,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_store.TryGetValue(userId, out var list))
                return Task.FromResult<IReadOnlyList<VerbatimMemory>>([]);

            return Task.FromResult<IReadOnlyList<VerbatimMemory>>(
                list.OrderByDescending(x => x.CreatedAt).Take(limit).ToList());
        }
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
    {
        lock (_lock)
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
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync(string userId, CancellationToken ct = default)
    {
        lock (_lock) _store.TryRemove(userId, out _);
        return Task.CompletedTask;
    }
}
