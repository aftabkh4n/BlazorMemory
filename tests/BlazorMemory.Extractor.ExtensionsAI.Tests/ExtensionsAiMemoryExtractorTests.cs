using BlazorMemory.Core.Models;
using BlazorMemory.Extractor.ExtensionsAI;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace BlazorMemory.Extractor.ExtensionsAI.Tests;

public class ExtensionsAiMemoryExtractorTests
{
    private sealed class FakeChatClient : IChatClient
    {
        private readonly Queue<string> _responses = new();
        public int CallCount { get; private set; }

        public FakeChatClient(params string[] responses)
        {
            foreach (var r in responses) _responses.Enqueue(r);
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var text = _responses.Count > 0 ? _responses.Dequeue() : "[]";
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
            return Task.FromResult(response);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    [Fact]
    public async Task ExtractFacts_Parses_Plain_JsonArray()
    {
        var client = new FakeChatClient("[\"User loves pizza.\", \"User uses C#.\"]");
        var sut    = new ExtensionsAiMemoryExtractor(client);

        var facts = await sut.ExtractFactsAsync("hello");

        facts.Should().Equal("User loves pizza.", "User uses C#.");
        client.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task ExtractFacts_Handles_Fenced_Json()
    {
        var client = new FakeChatClient("```json\n[\"User is a dev.\"]\n```");
        var sut    = new ExtensionsAiMemoryExtractor(client);

        var facts = await sut.ExtractFactsAsync("hello");

        facts.Should().Equal("User is a dev.");
    }

    [Fact]
    public async Task ExtractFacts_Retries_Once_When_First_Response_Unparseable()
    {
        var client = new FakeChatClient("this is not json at all", "[\"User is a dev.\"]");
        var sut    = new ExtensionsAiMemoryExtractor(client);

        var facts = await sut.ExtractFactsAsync("hello");

        facts.Should().Equal("User is a dev.");
        client.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Consolidate_Parses_UpdateDecision()
    {
        var client = new FakeChatClient(
            "{\"action\":\"UPDATE\",\"targetId\":\"m1\",\"updatedContent\":\"User is a senior C# dev.\"}");
        var sut = new ExtensionsAiMemoryExtractor(client);

        var similar = new List<MemoryEntry>
        {
            new()
            {
                Id = "m1", UserId = "u1", Content = "User is a dev.",
                Embedding = [0.1f], LearnedAt = DateTimeOffset.UtcNow
            }
        };

        var decision = await sut.ConsolidateAsync("User is a senior C# dev.", similar);

        decision.Action.Should().Be(ConsolidationAction.Update);
        decision.TargetMemoryId.Should().Be("m1");
        decision.UpdatedContent.Should().Be("User is a senior C# dev.");
    }
}
