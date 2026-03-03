using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;

namespace BlazorMemory.Embeddings.OpenAi;

public sealed class OpenAiEmbeddingsOptions
{
    /// <summary>
    /// Can be empty at startup — the user enters it via the UI.
    /// The client is created lazily on first use.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model to use. Defaults to text-embedding-3-small (1536 dims, cheapest).
    /// Use text-embedding-3-large for higher quality (3072 dims).
    /// </summary>
    public string Model { get; set; } = "text-embedding-3-small";
}

/// <summary>
/// Embeddings provider backed by OpenAI's text-embedding models.
/// The OpenAI client is created lazily on first use so the app can start
/// before the user has entered their API key.
/// </summary>
public sealed class OpenAiEmbeddingsProvider : IEmbeddingsProvider
{
    private readonly OpenAiEmbeddingsOptions _options;

    private static readonly Dictionary<string, int> ModelDimensions = new()
    {
        ["text-embedding-3-small"] = 1536,
        ["text-embedding-3-large"] = 3072,
        ["text-embedding-ada-002"] = 1536,
    };

    public int Dimensions =>
        ModelDimensions.TryGetValue(_options.Model, out var d) ? d : 1536;

    public OpenAiEmbeddingsProvider(IOptions<OpenAiEmbeddingsOptions> options)
    {
        _options = options.Value;
        // Do NOT create the OpenAI client here — key may be empty at startup
    }

    private EmbeddingClient CreateClient()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException(
                "OpenAI API key is not configured. Enter your key in the app config panel.");

        return new OpenAIClient(_options.ApiKey).GetEmbeddingClient(_options.Model);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var client = CreateClient();
        var response = await client.GenerateEmbeddingAsync(text, cancellationToken: ct);
        return response.Value.ToFloats().ToArray();
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IEnumerable<string> texts,
        CancellationToken ct = default)
    {
        var client = CreateClient();
        var list = texts.ToList();
        var response = await client.GenerateEmbeddingsAsync(list, cancellationToken: ct);
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
            o.Model = model;
        });
        builder.Services.AddScoped<IEmbeddingsProvider, OpenAiEmbeddingsProvider>();
        return builder;
    }

    public static BlazorMemoryBuilder UseOpenAiEmbeddings(
        this BlazorMemoryBuilder builder,
        Action<OpenAiEmbeddingsOptions> configure)
    {
        builder.Services.Configure(configure);
        builder.Services.AddScoped<IEmbeddingsProvider, OpenAiEmbeddingsProvider>();
        return builder;
    }
}