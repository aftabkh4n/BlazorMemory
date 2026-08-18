using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Engine;
using BlazorMemory.Core.Models;
using BlazorMemory.Core.Services;
using BlazorMemory.Storage.InMemory;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BlazorMemory.Core.Tests;

public class AgentMemoryServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AgentMemoryService BuildWithMock(
        IMemoryService memory,
        string agentId = "agent_planner",
        string sharedUserId = "team_alpha")
        => new(agentId, sharedUserId, memory);

    private static (AgentMemoryServiceFactory factory, InMemoryMemoryStore store) BuildRealStack()
    {
        var store     = new InMemoryMemoryStore();
        var embeddings = Substitute.For<IEmbeddingsProvider>();
        var extractor  = Substitute.For<IMemoryExtractor>();

        embeddings.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([0.1f, 0.2f, 0.3f]);

        var engine  = new ExtractionEngine(store, embeddings, extractor,
                          NullLogger<ExtractionEngine>.Instance);
        var service = new MemoryService(store, embeddings, engine,
                          NullLogger<MemoryService>.Instance);

        return (new AgentMemoryServiceFactory(service), store);
    }

    private static MemoryEntry MakeEntry(string id, string userId, string content, string? ns = null) =>
        new()
        {
            Id        = id,
            UserId    = userId,
            Content   = content,
            Embedding = [0.1f, 0.2f, 0.3f],
            LearnedAt = DateTimeOffset.UtcNow,
            Namespace = ns
        };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_StoresWithAgentNamespace()
    {
        var memory = Substitute.For<IMemoryService>();
        var agent  = BuildWithMock(memory, agentId: "agent_planner");

        await agent.ExtractAsync("User: I like to plan things.");

        await memory.Received(1).ExtractAsync(
            Arg.Any<string>(),
            "team_alpha",
            "agent_planner",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_QueriesAllNamespaces()
    {
        var memory = Substitute.For<IMemoryService>();
        memory.QueryAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<QueryOptions?>(), Arg.Any<CancellationToken>())
              .Returns([]);
        var agent = BuildWithMock(memory, agentId: "agent_planner");

        await agent.QueryAsync("context");

        await memory.Received(1).QueryAsync(
            Arg.Any<string>(),
            "team_alpha",
            Arg.Is<QueryOptions?>(o => o != null && o.Namespace == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryOwnAsync_QueriesOwnNamespaceOnly()
    {
        var memory = Substitute.For<IMemoryService>();
        memory.QueryAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<QueryOptions?>(), Arg.Any<CancellationToken>())
              .Returns([]);
        var agent = BuildWithMock(memory, agentId: "agent_coder");

        await agent.QueryOwnAsync("context");

        await memory.Received(1).QueryAsync(
            Arg.Any<string>(),
            "team_alpha",
            Arg.Is<QueryOptions?>(o => o != null && o.Namespace == "agent_coder"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TwoAgents_ShareSamePool_BothSeeEachOthersMemories()
    {
        var (factory, store) = BuildRealStack();
        var planner = factory.CreateAgent("agent_planner", "team_alpha");
        var coder   = factory.CreateAgent("agent_coder",   "team_alpha");

        await store.AddAsync(MakeEntry("p1", "team_alpha", "User prefers top-down planning", "agent_planner"));
        await store.AddAsync(MakeEntry("c1", "team_alpha", "User writes in C#",              "agent_coder"));

        var fromPlanner = await planner.ListAllAsync();
        var fromCoder   = await coder.ListAllAsync();

        fromPlanner.Should().HaveCount(2);
        fromCoder.Should().HaveCount(2);
    }

    [Fact]
    public async Task ClearOwnAsync_DoesNotAffectOtherAgent()
    {
        var (factory, store) = BuildRealStack();
        var planner = factory.CreateAgent("agent_planner", "team_alpha");
        var coder   = factory.CreateAgent("agent_coder",   "team_alpha");

        await store.AddAsync(MakeEntry("p1", "team_alpha", "User prefers top-down planning", "agent_planner"));
        await store.AddAsync(MakeEntry("c1", "team_alpha", "User writes in C#",              "agent_coder"));

        await planner.ClearOwnAsync();

        var plannerOwn = await planner.ListOwnAsync();
        var coderOwn   = await coder.ListOwnAsync();

        plannerOwn.Should().BeEmpty();
        coderOwn.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListAllAsync_ReturnsMemoriesFromAllAgents()
    {
        var memory = Substitute.For<IMemoryService>();
        memory.ListAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
              .Returns([]);
        var agent = BuildWithMock(memory, agentId: "agent_planner");

        await agent.ListAllAsync();

        await memory.Received(1).ListAsync(
            "team_alpha",
            null,
            Arg.Any<CancellationToken>());
    }
}
