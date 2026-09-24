using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using BlazorMemory.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BlazorMemory.Core.Tests;

public class ExtractionModeTests
{
    private static IMemoryService BuildMemory()
    {
        var memory = Substitute.For<IMemoryService>();
        memory.QueryAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<QueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MemoryEntry>());
        return memory;
    }

    [Fact]
    public async Task Inline_Default_AwaitsExtraction_And_ReturnsReply()
    {
        var memory  = BuildMemory();
        var options = new ExtractionOptions { Mode = ExtractionMode.Inline };
        var chat    = new MemoryEnabledChat(memory, NullLogger<MemoryEnabledChat>.Instance, options);

        var reply = await chat.ChatAsync("hi", "user1", (_, _) => Task.FromResult("hello"));

        reply.Should().Be("hello");
        await memory.Received(1).ExtractAsync(
            Arg.Any<string>(), "user1", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Inline_Timeout_LogsAndReturnsReply()
    {
        var memory = BuildMemory();
        memory.ExtractAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var token = ci.Arg<CancellationToken>();
                await Task.Delay(TimeSpan.FromSeconds(5), token);
            });

        var options = new ExtractionOptions
        {
            Mode              = ExtractionMode.Inline,
            ExtractionTimeout = TimeSpan.FromMilliseconds(50)
        };

        var logger = Substitute.For<ILogger<MemoryEnabledChat>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        var chat = new MemoryEnabledChat(memory, logger, options);

        var reply = await chat.ChatAsync("hi", "user1", (_, _) => Task.FromResult("hello"));

        reply.Should().Be("hello");
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Inline_ExtractionThrows_LogsAndReturnsReply()
    {
        var memory = BuildMemory();
        memory.ExtractAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var options = new ExtractionOptions { Mode = ExtractionMode.Inline };
        var chat = new MemoryEnabledChat(memory, NullLogger<MemoryEnabledChat>.Instance, options);

        var reply = await chat.ChatAsync("hi", "user1", (_, _) => Task.FromResult("still ok"));

        reply.Should().Be("still ok");
    }

    [Fact]
    public async Task Background_EnqueuesWithoutAwaitingExtraction()
    {
        var memory = BuildMemory();
        var queue  = Substitute.For<IMemoryExtractionQueue>();
        queue.TryEnqueue(Arg.Any<MemoryExtractionWorkItem>()).Returns(true);

        var options = new ExtractionOptions { Mode = ExtractionMode.Background };
        var chat = new MemoryEnabledChat(
            memory, NullLogger<MemoryEnabledChat>.Instance, options, queue);

        var reply = await chat.ChatAsync("hi", "user1", (_, _) => Task.FromResult("hello"));

        reply.Should().Be("hello");
        queue.Received(1).TryEnqueue(Arg.Is<MemoryExtractionWorkItem>(
            item => item.UserId == "user1" && item.Conversation.Contains("hello")));
        await memory.DidNotReceive().ExtractAsync(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Background_ChannelQueue_Drops_When_Full_WithoutThrowing()
    {
        var options = new ExtractionOptions { BackgroundQueueCapacity = 1 };
        var queue = new ChannelMemoryExtractionQueue(
            options, NullLogger<ChannelMemoryExtractionQueue>.Instance);

        queue.TryEnqueue(new MemoryExtractionWorkItem("a", "u", null)).Should().BeTrue();
        queue.TryEnqueue(new MemoryExtractionWorkItem("b", "u", null)).Should().BeFalse();
    }

    [Fact]
    public async Task Worker_Drains_Queue_And_Calls_ExtractAsync()
    {
        var memory = Substitute.For<IMemoryService>();
        memory.ExtractAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(memory);
        var provider = services.BuildServiceProvider();

        var options = new ExtractionOptions { BackgroundQueueCapacity = 4 };
        var queue   = new ChannelMemoryExtractionQueue(
            options, NullLogger<ChannelMemoryExtractionQueue>.Instance);

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var worker = new MemoryExtractionWorker(
            queue, scopeFactory, NullLogger<MemoryExtractionWorker>.Instance);

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);

        queue.TryEnqueue(new MemoryExtractionWorkItem("conv1", "user1", null));
        queue.TryEnqueue(new MemoryExtractionWorkItem("conv2", "user2", "ns"));

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var got = memory.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "ExtractAsync");
            if (got >= 2) break;
            await Task.Delay(25);
        }

        await worker.StopAsync(CancellationToken.None);

        await memory.Received(1).ExtractAsync("conv1", "user1", null, Arg.Any<CancellationToken>());
        await memory.Received(1).ExtractAsync("conv2", "user2", "ns", Arg.Any<CancellationToken>());
    }
}
