using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BlazorMemory.Core.Abstractions;
using Microsoft.Extensions.Options;

namespace BlazorMemory.Embeddings.Ollama;

public sealed class OllamaEmbeddingsProvider : IEmbeddingsProvider
{
    private readonly HttpClient _http;
    private readonly OllamaEmbeddingsOptions _options;

    public int Dimensions => _options.Dimensions;

    public OllamaEmbeddingsProvider(HttpClient http, IOptions<OllamaEmbeddingsOptions> options)
    {
        _options = options.Value;
        _http = http;
        _http.BaseAddress ??= new Uri(_options.BaseUrl);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var request = new EmbeddingRequest(_options.Model, text);
        var response = await _http.PostAsJsonAsync("/api/embeddings", request, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(ct);
        return result?.Embedding ?? [];
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
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt);

    private sealed record EmbeddingResponse(
        [property: JsonPropertyName("embedding")] float[] Embedding);
}
