using System.Net;
using System.Text;
using BlazorMemory.Embeddings.Ollama;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorMemory.Embeddings.Ollama.Tests;

public class OllamaEmbeddingsProviderTests
{
    private static OllamaEmbeddingsProvider Build(string responseJson, out CapturingHandler handler)
        => Build(responseJson, new OllamaEmbeddingsOptions(), out handler);

    private static OllamaEmbeddingsProvider Build(
        string responseJson,
        OllamaEmbeddingsOptions options,
        out CapturingHandler handler)
    {
        handler = new CapturingHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        return new OllamaEmbeddingsProvider(http, Options.Create(options));
    }

    [Fact]
    public async Task EmbedAsync_ReturnsCorrectFloatArray()
    {
        var provider = Build("""{"embedding":[0.1,0.2,0.3]}""", out _);

        var result = await provider.EmbedAsync("hello world");

        result.Should().HaveCount(3);
        result[0].Should().BeApproximately(0.1f, 0.0001f);
        result[1].Should().BeApproximately(0.2f, 0.0001f);
        result[2].Should().BeApproximately(0.3f, 0.0001f);
    }

    [Fact]
    public async Task EmbedAsync_UsesCorrectModelName()
    {
        var provider = Build(
            """{"embedding":[0.1]}""",
            new OllamaEmbeddingsOptions { Model = "custom-model" },
            out var handler);


        await provider.EmbedAsync("test text");

        handler.LastBody.Should().Contain("\"custom-model\"");
    }

    [Fact]
    public async Task EmbedAsync_CallsCorrectEndpoint()
    {
        var provider = Build("""{"embedding":[0.1]}""", out var handler);

        await provider.EmbedAsync("test text");

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/embeddings");
    }

    [Fact]
    public async Task EmbedBatchAsync_ReturnsCorrectNumberOfEmbeddings()
    {
        var provider = Build("""{"embedding":[0.1,0.2,0.3]}""", out _);

        var results = await provider.EmbedBatchAsync(["alpha", "beta", "gamma"]);

        results.Should().HaveCount(3);
        results[0].Should().HaveCount(3);
    }

    [Fact]
    public void Dimensions_ReturnsConfiguredValue()
    {
        var provider = Build(
            "{}",
            new OllamaEmbeddingsOptions { Dimensions = 1024 },
            out _);

        provider.Dimensions.Should().Be(1024);
    }
}

/// <summary>Test double that captures the last outgoing HTTP request.</summary>
public sealed class CapturingHandler : HttpMessageHandler
{
    private readonly string _responseJson;
    private readonly HttpStatusCode _statusCode;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string LastBody { get; private set; } = string.Empty;

    public CapturingHandler(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responseJson = responseJson;
        _statusCode = statusCode;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content is not null)
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
        };
    }
}
