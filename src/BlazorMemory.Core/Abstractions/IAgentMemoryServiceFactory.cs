using BlazorMemory.Core.Services;

namespace BlazorMemory.Core.Abstractions;

public interface IAgentMemoryServiceFactory
{
    AgentMemoryService CreateAgent(string agentId, string sharedUserId);
}
