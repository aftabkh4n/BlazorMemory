using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;

namespace BlazorMemory.Core.Services;

public sealed class AgentMemoryService
{
    private readonly IMemoryService _memory;

    public string AgentId { get; }
    public string SharedUserId { get; }

    public AgentMemoryService(string agentId, string sharedUserId, IMemoryService memory)
    {
        AgentId      = agentId;
        SharedUserId = sharedUserId;
        _memory      = memory;
    }

    public Task ExtractAsync(string conversation, CancellationToken ct = default)
        => _memory.ExtractAsync(conversation, SharedUserId, AgentId, ct);

    public Task<IReadOnlyList<MemoryEntry>> QueryAsync(
        string context, QueryOptions? options = null, CancellationToken ct = default)
    {
        var opts = (options ?? new QueryOptions()) with { Namespace = null };
        return _memory.QueryAsync(context, SharedUserId, opts, ct);
    }

    public Task<IReadOnlyList<MemoryEntry>> QueryOwnAsync(
        string context, QueryOptions? options = null, CancellationToken ct = default)
    {
        var opts = (options ?? new QueryOptions()) with { Namespace = AgentId };
        return _memory.QueryAsync(context, SharedUserId, opts, ct);
    }

    public Task<IReadOnlyList<MemoryEntry>> ListAllAsync(CancellationToken ct = default)
        => _memory.ListAsync(SharedUserId, null, ct);

    public Task<IReadOnlyList<MemoryEntry>> ListOwnAsync(CancellationToken ct = default)
        => _memory.ListAsync(SharedUserId, AgentId, ct);

    public Task DeleteAsync(string id, CancellationToken ct = default)
        => _memory.DeleteAsync(id, ct);

    public Task ClearOwnAsync(CancellationToken ct = default)
        => _memory.ClearAsync(SharedUserId, AgentId, ct);
}
