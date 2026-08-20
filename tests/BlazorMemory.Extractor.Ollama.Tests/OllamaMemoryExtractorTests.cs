using System.Net;
using System.Text;
using BlazorMemory.Core.Models;
using BlazorMemory.Extractor.Ollama;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorMemory.Extractor.Ollama.Tests;

public class OllamaMemoryExtractorTests
{
    private static MemoryEntry MakeEntry(string id, string content) => new()
    {
        Id = id,
        UserId = "u1",
        Content = content,
        Embedding = [],
        LearnedAt = DateTimeOffset.UtcNow
    };

    private static OllamaMemoryExtractor Build(string assistantContent)
    {
        var serialized = System.Text.Json.JsonSerializer.Serialize(assistantContent);
        var responseJson = $"{{\"message\":{{\"role\":\"assistant\",\"content\":{serialized}}}}}";
        var handler = new StubHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        return new OllamaMemoryExtractor(http, Options.Create(new OllamaExtractorOptions()));
    }

    private static OllamaMemoryExtractor BuildMulti(params string[] assistantContents)
    {
        var responses = assistantContents.Select(c =>
        {
            var serialized = System.Text.Json.JsonSerializer.Serialize(c);
            return $"{{\"message\":{{\"role\":\"assistant\",\"content\":{serialized}}}}}";
        }).ToArray();
        var handler = new StubHandlerSequential(responses);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        return new OllamaMemoryExtractor(http, Options.Create(new OllamaExtractorOptions()));
    }

    [Fact]
    public async Task ExtractFactsAsync_ReturnsFacts_WhenLlmReturnsJsonArray()
    {
        var extractor = Build("""["User is a developer","User likes pizza"]""");

        var result = await extractor.ExtractFactsAsync("Some conversation");

        result.Should().HaveCount(2);
        result[0].Should().Be("User is a developer");
        result[1].Should().Be("User likes pizza");
    }

    [Fact]
    public async Task ExtractFactsAsync_ReturnsEmpty_WhenLlmReturnsEmptyArray()
    {
        var extractor = Build("[]");

        var result = await extractor.ExtractFactsAsync("No facts here");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFactsAsync_HandlesInvalidJson_Gracefully()
    {
        var extractor = Build("Sorry, I cannot extract any facts from this.");

        var result = await extractor.ExtractFactsAsync("Some conversation");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ConsolidateAsync_ReturnsAdd_WhenActionIsAdd()
    {
        var extractor = Build("""{"action":"ADD"}""");
        var memories = new[] { MakeEntry("1", "User is a developer") };

        var decision = await extractor.ConsolidateAsync("User likes cats", memories);

        decision.Action.Should().Be(ConsolidationAction.Add);
    }

    [Fact]
    public async Task ConsolidateAsync_ReturnsNone_WhenActionIsNone()
    {
        var extractor = Build("""{"action":"NONE"}""");
        var memories = new[] { MakeEntry("1", "User is a developer") };

        var decision = await extractor.ConsolidateAsync("User is a developer", memories);

        decision.Action.Should().Be(ConsolidationAction.None);
    }

    [Fact]
    public async Task ConsolidateAsync_ReturnsUpdate_WithTargetIdAndContent()
    {
        var extractor = Build("""{"action":"UPDATE","targetId":"42","content":"User is a senior developer"}""");
        var memories = new[] { MakeEntry("42", "User is a developer") };

        var decision = await extractor.ConsolidateAsync("User is a senior developer", memories);

        decision.Action.Should().Be(ConsolidationAction.Update);
        decision.TargetMemoryId.Should().Be("42");
        decision.UpdatedContent.Should().Be("User is a senior developer");
    }

    [Fact]
    public async Task SummarizeAsync_ReturnsSummaryString()
    {
        var extractor = Build("User background: User is a developer who likes pizza.");
        var memories = new[]
        {
            MakeEntry("1", "User is a developer"),
            MakeEntry("2", "User likes pizza")
        };

        var result = await extractor.SummarizeAsync(memories);

        result.Should().StartWith("User background:");
    }

    [Fact]
    public async Task ExtractFactsAsync_HandlesMarkdownCodeFences()
    {
        var extractor = Build("```json\n[\"User is a developer\",\"User likes pizza\"]\n```");

        var result = await extractor.ExtractFactsAsync("Some conversation");

        result.Should().HaveCount(2);
        result[0].Should().Be("User is a developer");
    }

    [Fact]
    public async Task ExtractFactsAsync_HandlesTextBeforeAndAfterJson()
    {
        var extractor = Build("Here are the facts:\n[\"User is a developer\"]\nHope that helps!");

        var result = await extractor.ExtractFactsAsync("Some conversation");

        result.Should().ContainSingle().Which.Should().Be("User is a developer");
    }

    [Fact]
    public async Task ExtractFactsAsync_RetriesOnParseFailure()
    {
        var extractor = BuildMulti(
            "I cannot determine any facts.",
            "[\"User prefers dark mode\"]");

        var result = await extractor.ExtractFactsAsync("Some conversation");

        result.Should().ContainSingle().Which.Should().Be("User prefers dark mode");
    }

    [Fact]
    public async Task ConsolidateAsync_HandlesMarkdownCodeFences()
    {
        var extractor = Build("```json\n{\"action\":\"ADD\"}\n```");
        var memories = new[] { MakeEntry("1", "User is a developer") };

        var decision = await extractor.ConsolidateAsync("User likes cats", memories);

        decision.Action.Should().Be(ConsolidationAction.Add);
    }
}

public sealed class StubHandler : HttpMessageHandler
{
    private readonly string _responseJson;

    public StubHandler(string responseJson) => _responseJson = responseJson;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
        });
}

public sealed class StubHandlerSequential : HttpMessageHandler
{
    private readonly Queue<string> _responses;

    public StubHandlerSequential(params string[] responses) =>
        _responses = new Queue<string>(responses);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var json = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }
}
