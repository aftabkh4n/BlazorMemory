using Xunit;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using BlazorMemory.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace BlazorMemory.Core.Tests;

public class MemoryEnabledChatTests
{
    private static float[] FakeEmbedding() => [0.1f, 0.2f, 0.3f];

    private static (MemoryEnabledChat sut, IMemoryService memory) BuildSut()
    {
        var memory = Substitute.For<IMemoryService>();

        memory.QueryAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<QueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MemoryEntry>());

        var sut = new MemoryEnabledChat(memory, NullLogger<MemoryEnabledChat>.Instance);
        return (sut, memory);
    }

    private static MemoryEntry MakeEntry(string id, string content) => new()
    {
        Id        = id,
        UserId    = "user1",
        Content   = content,
        Embedding = FakeEmbedding(),
        LearnedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task ChatAsync_Returns_LlmReply()
    {
        var (sut, _) = BuildSut();

        var result = await sut.ChatAsync("hello", "user1", (_, _) => Task.FromResult("Hi there!"));

        result.Should().Be("Hi there!");
    }

    [Fact]
    public async Task ChatAsync_Queries_Memories_Before_LlmCall()
    {
        var (sut, memory) = BuildSut();
        var callOrder     = new List<string>();

        memory.QueryAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<QueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(_ => { callOrder.Add("query"); return Array.Empty<MemoryEntry>(); });

        await sut.ChatAsync("hello", "user1", (_, _) =>
        {
            callOrder.Add("llm");
            return Task.FromResult("reply");
        });

        callOrder.Should().Equal("query", "llm");
    }

    [Fact]
    public async Task ChatAsync_Passes_UserMessage_To_QueryAsync()
    {
        var (sut, memory) = BuildSut();

        await sut.ChatAsync("What is the weather?", "user1", (_, _) => Task.FromResult("reply"));

        await memory.Received(1).QueryAsync(
            "What is the weather?", "user1",
            Arg.Any<QueryOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChatAsync_InjectsMemories_Into_SystemPrompt()
    {
        var (sut, memory) = BuildSut();

        memory.QueryAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<QueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new[] { MakeEntry("1", "User loves pizza") });

        string? capturedSystemPrompt = null;
        await sut.ChatAsync("hello", "user1", (sp, _) =>
        {
            capturedSystemPrompt = sp;
            return Task.FromResult("reply");
        });

        capturedSystemPrompt.Should().Contain("User loves pizza");
    }

    [Fact]
    public async Task ChatAsync_BasePrompt_Returned_When_No_Memories()
    {
        var (sut, _) = BuildSut();

        string? capturedSystemPrompt = null;
        await sut.ChatAsync("hello", "user1", (sp, _) =>
        {
            capturedSystemPrompt = sp;
            return Task.FromResult("reply");
        });

        capturedSystemPrompt.Should().NotContain("What you remember about this user");
        capturedSystemPrompt.Should().Contain("persistent memory");
    }

    [Fact]
    public async Task ChatAsync_Passes_UserMessage_To_LlmCall()
    {
        var (sut, _) = BuildSut();

        string? capturedMsg = null;
        await sut.ChatAsync("my question", "user1", (_, msg) =>
        {
            capturedMsg = msg;
            return Task.FromResult("reply");
        });

        capturedMsg.Should().Be("my question");
    }

    [Fact]
    public async Task ChatAsync_Calls_ExtractAsync_With_Conversation()
    {
        var (sut, memory) = BuildSut();

        await sut.ChatAsync("hello", "user1", (_, _) => Task.FromResult("assistant reply"));

        await memory.Received(1).ExtractAsync(
            Arg.Is<string>(s => s.Contains("hello") && s.Contains("assistant reply")),
            "user1",
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChatAsync_DoesNotThrow_When_Extraction_Fails()
    {
        var (sut, memory) = BuildSut();

        memory.ExtractAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("extractor down"));

        var act = () => sut.ChatAsync("hello", "user1", (_, _) => Task.FromResult("reply"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ChatAsync_StillReturnsReply_When_Extraction_Fails()
    {
        var (sut, memory) = BuildSut();

        memory.ExtractAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("extractor down"));

        var result = await sut.ChatAsync("hello", "user1", (_, _) => Task.FromResult("the answer"));

        result.Should().Be("the answer");
    }

    [Fact]
    public async Task ChatAsync_Applies_Namespace_To_Query()
    {
        var (sut, memory) = BuildSut();

        await sut.ChatAsync("hello", "user1", (_, _) => Task.FromResult("reply"), @namespace: "work");

        await memory.Received(1).QueryAsync(
            "hello", "user1",
            Arg.Is<QueryOptions?>(q => q != null && q.Namespace == "work"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChatAsync_Applies_Namespace_To_ExtractAsync()
    {
        var (sut, memory) = BuildSut();

        await sut.ChatAsync("hello", "user1", (_, _) => Task.FromResult("reply"), @namespace: "work");

        await memory.Received(1).ExtractAsync(
            Arg.Any<string>(), "user1", "work", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChatAsync_Respects_Custom_QueryOptions()
    {
        var (sut, memory) = BuildSut();

        sut.QueryOptions = new QueryOptions { Limit = 3, Threshold = 0.9f };

        await sut.ChatAsync("hello", "user1", (_, _) => Task.FromResult("reply"));

        await memory.Received(1).QueryAsync(
            "hello", "user1",
            Arg.Is<QueryOptions?>(q => q != null && q.Limit == 3 && q.Threshold == 0.9f),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChatAsync_Namespace_Param_Overrides_QueryOptions_Namespace()
    {
        var (sut, memory) = BuildSut();

        sut.QueryOptions = new QueryOptions { Namespace = "default" };

        await sut.ChatAsync("hello", "user1", (_, _) => Task.FromResult("reply"), @namespace: "override");

        await memory.Received(1).QueryAsync(
            "hello", "user1",
            Arg.Is<QueryOptions?>(q => q != null && q.Namespace == "override"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChatAsync_CustomBaseSystemPrompt_IsUsed()
    {
        var (sut, _) = BuildSut();

        sut.BaseSystemPrompt = "You are a pirate.";

        string? capturedSp = null;
        await sut.ChatAsync("hello", "user1", (sp, _) => { capturedSp = sp; return Task.FromResult("reply"); });

        capturedSp.Should().Contain("You are a pirate.");
    }
}
