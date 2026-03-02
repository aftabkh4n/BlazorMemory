using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;

namespace BlazorMemory.Embeddings.OpenAi;

public sealed class OpenAiEmbeddingsOptions
{
    public required string ApiKey { get; set; }

    /// <summary>
    /// Model to use. Defaults to text-embedding-3-small (1536 dims, cheapest).
    /// Use text-embedding-3-large for higher quality (3072 dims).
    /// </summary>
    public string Model { get; set; } = "text-embedding-3-small";
}

/// <summary>
/// Embeddings provider backed by OpenAI's text-embedding models.
/// </summary>
public sealed class OpenAiEmbeddingsProvider : IEmbeddingsProvider
{
    private readonly EmbeddingClient _client;
    private readonly OpenAiEmbeddingsOptions _options;

    // Dimension map for known models
    private static readonly Dictionary<string, int> ModelDimensions = new()
    {
        ["text-embedding-3-small"] = 1536,
        ["text-embedding-3-large"] = 3072,
        ["text-embedding-ada-002"]  = 1536,
    };

    public int Dimensions =>
        ModelDimensions.TryGetValue(_options.Model, out var d) ? d : 1536;

    public OpenAiEmbeddingsProvider(IOptions<OpenAiEmbeddingsOptions> options)
    {
        _options = options.Value;
        var openAiClient = new OpenAIClient(_options.ApiKey);
        _client = openAiClient.GetEmbeddingClient(_options.Model);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var response = await _client.GenerateEmbeddingAsync(text, cancellationToken: ct);
        return response.Value.ToFloats().ToArray();
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IEnumerable<string> texts,
        CancellationToken ct = default)
    {
        var list = texts.ToList();
        var response = await _client.GenerateEmbeddingsAsync(list, cancellationToken: ct);
        return response.Value
            .Select(e => e.ToFloats().ToArray())
            .ToList();
    }
}

/// <summary>DI registration extensions.</summary>
public static class OpenAiEmbeddingsExtensions
{
    public static BlazorMemoryBuilder UseOpenAiEmbeddings(
        this BlazorMemoryBuilder builder,
        string apiKey,
        string model = "text-embedding-3-small")
    {
        builder.Services.Configure<OpenAiEmbeddingsOptions>(o =>
        {
            o.ApiKey = apiKey;
            o.Model  = model;
        });
        builder.Services.AddScoped<IEmbeddingsProvider, OpenAiEmbeddingsProvider>();
        return builder;
    }

    /// <summary>Configure via options pattern (e.g. from appsettings).</summary>
    public static BlazorMemoryBuilder UseOpenAiEmbeddings(
        this BlazorMemoryBuilder builder,
        Action<OpenAiEmbeddingsOptions> configure)
    {
        builder.Services.Configure(configure);
        builder.Services.AddScoped<IEmbeddingsProvider, OpenAiEmbeddingsProvider>();
        return builder;
    }
}
