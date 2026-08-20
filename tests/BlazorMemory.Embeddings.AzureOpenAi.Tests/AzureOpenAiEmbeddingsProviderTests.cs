using System.Net;
using System.Text;
using BlazorMemory.Embeddings.AzureOpenAi;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorMemory.Embeddings.AzureOpenAi.Tests;

public class AzureOpenAiEmbeddingsProviderTests
{
    private static readonly AzureOpenAiEmbeddingsOptions DefaultOptions = new()
    {
        Endpoint = "https://myresource.openai.azure.com/",
        ApiKey = "test-key",
        DeploymentName = "text-embedding-3-small",
        ApiVersion = "2024-10-21",
        Dimensions = 1536
    };

    private static AzureOpenAiEmbeddingsProvider Build(string responseJson, out CapturingHandler handler,
        AzureOpenAiEmbeddingsOptions? options = null)
    {
        handler = new CapturingHandler(responseJson);
        var http = new HttpClient(handler);
        return new AzureOpenAiEmbeddingsProvider(http, Options.Create(options ?? DefaultOptions));
    }

    [Fact]
    public async Task EmbedAsync_ReturnsCorrectFloatArray()
    {
        var provider = Build("""{"data":[{"embedding":[0.1,0.2,0.3]}]}""", out _);

        var result = await provider.EmbedAsync("hello world");

        result.Should().HaveCount(3);
        result[0].Should().BeApproximately(0.1f, 0.0001f);
        result[1].Should().BeApproximately(0.2f, 0.0001f);
        result[2].Should().BeApproximately(0.3f, 0.0001f);
    }

    [Fact]
    public async Task EmbedAsync_UsesCorrectEndpointUrlWithDeploymentAndApiVersion()
    {
        var provider = Build("""{"data":[{"embedding":[0.1]}]}""", out var handler);

        await provider.EmbedAsync("test text");

        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("/openai/deployments/text-embedding-3-small/embeddings");
        url.Should().Contain("api-version=2024-10-21");
        url.Should().StartWith("https://myresource.openai.azure.com/");
    }

    [Fact]
    public void Dimensions_ReturnsConfiguredValue()
    {
        var provider = Build(
            "{}",
            out _,
            new AzureOpenAiEmbeddingsOptions { Dimensions = 3072 });

        provider.Dimensions.Should().Be(3072);
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
