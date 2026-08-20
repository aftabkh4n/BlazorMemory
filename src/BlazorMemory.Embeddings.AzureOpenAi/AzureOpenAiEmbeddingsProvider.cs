using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BlazorMemory.Core.Abstractions;
using Microsoft.Extensions.Options;

namespace BlazorMemory.Embeddings.AzureOpenAi;

public sealed class AzureOpenAiEmbeddingsProvider : IEmbeddingsProvider
{
    private readonly HttpClient _http;
    private readonly AzureOpenAiEmbeddingsOptions _options;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public int Dimensions => _options.Dimensions;

    public AzureOpenAiEmbeddingsProvider(HttpClient http, IOptions<AzureOpenAiEmbeddingsOptions> options)
    {
        _options = options.Value;
        _http = http;
    }

    private string EmbeddingsUrl =>
        $"{_options.Endpoint.TrimEnd('/')}/openai/deployments/{_options.DeploymentName}/embeddings?api-version={_options.ApiVersion}";

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, EmbeddingsUrl);
        req.Headers.Add("api-key", _options.ApiKey);
        req.Content = JsonContent.Create(new EmbeddingRequest(text));

        var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(JsonOpts, ct);
        return result?.Data?[0]?.Embedding ?? [];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IEnumerable<string> texts, CancellationToken ct = default)
    {
        var results = new List<float[]>();
        foreach (var text in texts)
            results.Add(await EmbedAsync(text, ct));
        return results;
    }

    private sealed record EmbeddingRequest(
        [property: JsonPropertyName("input")] string Input);

    private sealed record EmbeddingResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<EmbeddingData>? Data);

    private sealed record EmbeddingData(
        [property: JsonPropertyName("embedding")] float[] Embedding);
}
