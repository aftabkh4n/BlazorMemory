using System.Reflection;
using BlazorMemory.AgentFramework;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BlazorMemory.AgentFramework.Tests;

public class BlazorMemoryContextProviderTests
{
    private static IServiceProvider EmptyServices() => new ServiceCollection().BuildServiceProvider();

    private static MemoryEntry MakeMemory(string id, string content) => new()
    {
        Id        = id,
        UserId    = "u1",
        Content   = content,
        Embedding = [0.1f, 0.2f],
        LearnedAt = DateTimeOffset.UtcNow
    };

    private static AIContextProvider.InvokingContext MakeInvokingContext(params ChatMessage[] messages)
    {
        var agent   = Substitute.For<AIAgent>();
        var session = Substitute.For<AgentSession>();
        var aiCtx   = new AIContext { Messages = messages };
        return new AIContextProvider.InvokingContext(agent, session, aiCtx);
    }

    private static AIContextProvider.InvokedContext MakeInvokedContext(
        IEnumerable<ChatMessage>? request,
        IEnumerable<ChatMessage>? response,
        Exception? error = null)
    {
        var agent   = Substitute.For<AIAgent>();
        var session = Substitute.For<AgentSession>();
        var req     = request ?? Array.Empty<ChatMessage>();
        if (error is not null)
            return new AIContextProvider.InvokedContext(agent, session, req, error);
        return new AIContextProvider.InvokedContext(agent, session, req, response ?? Array.Empty<ChatMessage>());
    }

    // Reflection to invoke protected members
    private static ValueTask<AIContext> Provide(BlazorMemoryContextProvider p, AIContextProvider.InvokingContext ctx)
    {
        var m = typeof(AIContextProvider).GetMethod("ProvideAIContextAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (ValueTask<AIContext>)m.Invoke(p, new object[] { ctx, CancellationToken.None })!;
    }

    private static ValueTask Store(BlazorMemoryContextProvider p, AIContextProvider.InvokedContext ctx)
    {
        var m = typeof(AIContextProvider).GetMethod("StoreAIContextAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (ValueTask)m.Invoke(p, new object[] { ctx, CancellationToken.None })!;
    }

    [Fact]
    public void Ctor_Throws_When_UserIdResolver_Missing()
    {
        var memory  = Substitute.For<IMemoryService>();
        var options = new BlazorMemoryContextProviderOptions();
        var act = () => new BlazorMemoryContextProvider(
            memory, options, EmptyServices(), NullLogger<BlazorMemoryContextProvider>.Instance);
        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("options");
    }

    [Fact]
    public async Task ProvideAIContext_Injects_Recalled_Memories()
    {
        var memory = Substitute.For<IMemoryService>();
        memory.QueryAsync(Arg.Any<string>(), "u1", Arg.Any<QueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new[] { MakeMemory("m1", "User loves pizza") });

        var options = new BlazorMemoryContextProviderOptions { UserIdResolver = _ => "u1" };
        var provider = new BlazorMemoryContextProvider(
            memory, options, EmptyServices(), NullLogger<BlazorMemoryContextProvider>.Instance);

        var ctx = MakeInvokingContext(new ChatMessage(ChatRole.User, "hello"));
        var result = await Provide(provider, ctx);

        result.Instructions.Should().NotBeNullOrEmpty();
        result.Instructions.Should().Contain("User loves pizza");
    }

    [Fact]
    public async Task ProvideAIContext_Injects_Nothing_When_No_Memories()
    {
        var memory = Substitute.For<IMemoryService>();
        memory.QueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MemoryEntry>());

        var options = new BlazorMemoryContextProviderOptions { UserIdResolver = _ => "u1" };
        var provider = new BlazorMemoryContextProvider(
            memory, options, EmptyServices(), NullLogger<BlazorMemoryContextProvider>.Instance);

        var ctx = MakeInvokingContext(new ChatMessage(ChatRole.User, "hello"));
        var result = await Provide(provider, ctx);

        result.Instructions.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ProvideAIContext_Recall_Exception_Does_Not_Fail_Run()
    {
        var memory = Substitute.For<IMemoryService>();
        memory.QueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryOptions?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("store down"));

        var options = new BlazorMemoryContextProviderOptions { UserIdResolver = _ => "u1" };
        var provider = new BlazorMemoryContextProvider(
            memory, options, EmptyServices(), NullLogger<BlazorMemoryContextProvider>.Instance);

        var ctx = MakeInvokingContext(new ChatMessage(ChatRole.User, "hello"));
        var result = await Provide(provider, ctx);

        result.Instructions.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task StoreAIContext_Runs_Extraction_By_Default()
    {
        var memory = Substitute.For<IMemoryService>();
        memory.ExtractAsync(Arg.Any<string>(), "u1", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var options = new BlazorMemoryContextProviderOptions { UserIdResolver = _ => "u1" };
        var provider = new BlazorMemoryContextProvider(
            memory, options, EmptyServices(), NullLogger<BlazorMemoryContextProvider>.Instance);

        var ctx = MakeInvokedContext(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            new[] { new ChatMessage(ChatRole.Assistant, "hello") });
        await Store(provider, ctx);

        await memory.Received(1).ExtractAsync(
            Arg.Is<string>(s => s.Contains("hi") && s.Contains("hello")),
            "u1", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoreAIContext_Skips_When_ExtractAfterRun_False()
    {
        var memory = Substitute.For<IMemoryService>();
        var options = new BlazorMemoryContextProviderOptions
        {
            UserIdResolver  = _ => "u1",
            ExtractAfterRun = false
        };
        var provider = new BlazorMemoryContextProvider(
            memory, options, EmptyServices(), NullLogger<BlazorMemoryContextProvider>.Instance);

        var ctx = MakeInvokedContext(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            new[] { new ChatMessage(ChatRole.Assistant, "hello") });
        await Store(provider, ctx);

        await memory.DidNotReceive().ExtractAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProvideAIContext_Passes_Namespace_To_QueryOptions()
    {
        var memory = Substitute.For<IMemoryService>();
        memory.QueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MemoryEntry>());

        var options = new BlazorMemoryContextProviderOptions
        {
            UserIdResolver = _ => "u1",
            Namespace      = "coach"
        };
        var provider = new BlazorMemoryContextProvider(
            memory, options, EmptyServices(), NullLogger<BlazorMemoryContextProvider>.Instance);

        var ctx = MakeInvokingContext(new ChatMessage(ChatRole.User, "hi"));
        _ = await Provide(provider, ctx);

        await memory.Received(1).QueryAsync(
            "hi", "u1",
            Arg.Is<QueryOptions?>(q => q != null && q.Namespace == "coach"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoreAIContext_Passes_Namespace_To_ExtractAsync()
    {
        var memory = Substitute.For<IMemoryService>();
        memory.ExtractAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var options = new BlazorMemoryContextProviderOptions
        {
            UserIdResolver = _ => "u1",
            Namespace      = "coach"
        };
        var provider = new BlazorMemoryContextProvider(
            memory, options, EmptyServices(), NullLogger<BlazorMemoryContextProvider>.Instance);

        var ctx = MakeInvokedContext(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            new[] { new ChatMessage(ChatRole.Assistant, "hello") });
        await Store(provider, ctx);

        await memory.Received(1).ExtractAsync(
            Arg.Any<string>(), "u1", "coach", Arg.Any<CancellationToken>());
    }
}
