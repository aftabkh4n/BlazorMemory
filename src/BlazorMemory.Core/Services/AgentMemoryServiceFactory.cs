using BlazorMemory.Core.Abstractions;

namespace BlazorMemory.Core.Services;

public sealed class AgentMemoryServiceFactory : IAgentMemoryServiceFactory
{
    private readonly IMemoryService _memory;

    public AgentMemoryServiceFactory(IMemoryService memory) => _memory = memory;

    public AgentMemoryService CreateAgent(string agentId, string sharedUserId)
        => new(agentId, sharedUserId, _memory);
}
