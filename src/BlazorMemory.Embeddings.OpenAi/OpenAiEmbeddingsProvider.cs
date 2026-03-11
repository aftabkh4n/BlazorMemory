using BlazorMemory.Core;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;

namespace BlazorMemory.Embeddings.OpenAi;

public sealed class OpenAiEmbeddingsOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "text-embedding-3-small";
}

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
    }

    private EmbeddingClient CreateClient()
    {
        var key = ApiKeyStore.Instance.HasKey ? ApiKeyStore.Instance.ApiKey : _options.ApiKey;
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "OpenAI API key is not configured. Enter your key in the app config panel.");
        return new OpenAIClient(key).GetEmbeddingClient(_options.Model);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var result = await CreateClient().GenerateEmbeddingAsync(text, cancellationToken: ct);
        return result.Value.ToFloats().ToArray();
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IEnumerable<string> texts, CancellationToken ct = default)
    {
        var results = await CreateClient().GenerateEmbeddingsAsync(texts.ToList(), cancellationToken: ct);
        return results.Value.Select(e => e.ToFloats().ToArray()).ToList();
    }
}

public static class OpenAiEmbeddingsExtensions
{
    public static BlazorMemoryBuilder UseOpenAiEmbeddings(
        this BlazorMemoryBuilder builder,
        string apiKey = "",
        string model = "text-embedding-3-small")
    {
        builder.Services.Configure<OpenAiEmbeddingsOptions>(o => { o.ApiKey = apiKey; o.Model = model; });
        builder.Services.AddScoped<IEmbeddingsProvider, OpenAiEmbeddingsProvider>();
        return builder;
    }
}