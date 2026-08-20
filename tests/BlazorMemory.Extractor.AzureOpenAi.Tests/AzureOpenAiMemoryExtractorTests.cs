using System.Net;
using System.Text;
using BlazorMemory.Core.Models;
using BlazorMemory.Extractor.AzureOpenAi;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorMemory.Extractor.AzureOpenAi.Tests;

public class AzureOpenAiMemoryExtractorTests
{
    private static readonly AzureOpenAiExtractorOptions DefaultOptions = new()
    {
        Endpoint = "https://myresource.openai.azure.com/",
        ApiKey = "test-key",
        DeploymentName = "gpt-4o-mini",
        ApiVersion = "2024-10-21"
    };

    private static MemoryEntry MakeEntry(string id, string content) => new()
    {
        Id = id,
        UserId = "u1",
        Content = content,
        Embedding = [],
        LearnedAt = DateTimeOffset.UtcNow
    };

    private static AzureOpenAiMemoryExtractor Build(string assistantContent, out CapturingHandler handler,
        AzureOpenAiExtractorOptions? options = null)
    {
        var serialized = System.Text.Json.JsonSerializer.Serialize(assistantContent);
        var responseJson = $"{{\"choices\":[{{\"message\":{{\"role\":\"assistant\",\"content\":{serialized}}}}}]}}";
        handler = new CapturingHandler(responseJson);
        var http = new HttpClient(handler);
        return new AzureOpenAiMemoryExtractor(http, Options.Create(options ?? DefaultOptions));
    }

    [Fact]
    public async Task ExtractFactsAsync_ReturnsFacts_WhenLlmReturnsJsonArray()
    {
        var extractor = Build("""["User is a developer","User likes C#"]""", out _);

        var result = await extractor.ExtractFactsAsync("Some conversation");

        result.Should().HaveCount(2);
        result[0].Should().Be("User is a developer");
        result[1].Should().Be("User likes C#");
    }

    [Fact]
    public async Task ConsolidateAsync_ReturnsAdd_WhenActionIsAdd()
    {
        var extractor = Build("""{"action":"ADD"}""", out _);
        var memories = new[] { MakeEntry("1", "User is a developer") };

        var decision = await extractor.ConsolidateAsync("User likes cats", memories);

        decision.Action.Should().Be(ConsolidationAction.Add);
    }

    [Fact]
    public async Task SummarizeAsync_ReturnsSummaryString()
    {
        var extractor = Build("User background: User is a developer who likes C#.", out _);
        var memories = new[]
        {
            MakeEntry("1", "User is a developer"),
            MakeEntry("2", "User likes C#")
        };

        var result = await extractor.SummarizeAsync(memories);

        result.Should().StartWith("User background:");
    }

    [Fact]
    public async Task ExtractFactsAsync_UsesCorrectEndpointUrlWithDeploymentAndApiVersion()
    {
        var extractor = Build("""["User is a developer"]""", out var handler);

        await extractor.ExtractFactsAsync("Some conversation");

        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("/openai/deployments/gpt-4o-mini/chat/completions");
        url.Should().Contain("api-version=2024-10-21");
        url.Should().StartWith("https://myresource.openai.azure.com/");
    }
}

public sealed class CapturingHandler : HttpMessageHandler
{
    private readonly string _responseJson;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string LastBody { get; private set; } = string.Empty;

    public CapturingHandler(string responseJson) => _responseJson = responseJson;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content is not null)
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
        };
    }
}
