using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorMemory.Embeddings.ExtensionsAI;

public sealed class ExtensionsAiEmbeddingsOptions
{
    /// <summary>
    /// Fallback embedding dimension used when the underlying
    /// <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> does not expose
    /// <see cref="EmbeddingGeneratorMetadata.DefaultModelDimensions"/>. Leave
    /// null to require the generator to advertise dimensions itself.
    /// </summary>
    public int? Dimensions { get; set; }
}

/// <summary>
/// Adapts any Microsoft.Extensions.AI
/// <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> (with TInput=string,
/// TEmbedding=Embedding&lt;float&gt;) to BlazorMemory's
/// <see cref="IEmbeddingsProvider"/>.
/// </summary>
public sealed class ExtensionsAiEmbeddingsProvider : IEmbeddingsProvider
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;

    public int    Dimensions      { get; }
    public string ModelIdentifier { get; }

    public ExtensionsAiEmbeddingsProvider(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        ExtensionsAiEmbeddingsOptions? options = null)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));

        var metadata = generator.GetService(typeof(EmbeddingGeneratorMetadata)) as EmbeddingGeneratorMetadata;

        ModelIdentifier = FormatModelIdentifier(metadata);
        Dimensions      = ResolveDimensions(metadata, options?.Dimensions);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var result = await _generator.GenerateAsync([text], options: null, ct).ConfigureAwait(false);
        return result[0].Vector.ToArray();
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IEnumerable<string> texts, CancellationToken ct = default)
    {
        var input   = texts as IList<string> ?? texts.ToList();
        var result  = await _generator.GenerateAsync(input, options: null, ct).ConfigureAwait(false);
        var vectors = new float[result.Count][];
        for (int i = 0; i < result.Count; i++)
            vectors[i] = result[i].Vector.ToArray();
        return vectors;
    }

    private static string FormatModelIdentifier(EmbeddingGeneratorMetadata? metadata)
    {
        if (metadata is null) return "extensionsai/unknown";
        var provider = string.IsNullOrWhiteSpace(metadata.ProviderName) ? "extensionsai" : metadata.ProviderName!;
        var model    = string.IsNullOrWhiteSpace(metadata.DefaultModelId) ? "unknown"     : metadata.DefaultModelId!;
        return $"{provider}/{model}";
    }

    private static int ResolveDimensions(EmbeddingGeneratorMetadata? metadata, int? optionOverride)
    {
        if (metadata?.DefaultModelDimensions is int dim && dim > 0) return dim;
        if (optionOverride is int over && over > 0)                 return over;
        throw new InvalidOperationException(
            "ExtensionsAiEmbeddingsProvider could not determine embedding dimensions. " +
            "The IEmbeddingGenerator did not expose EmbeddingGeneratorMetadata.DefaultModelDimensions " +
            "and no fallback was provided. Set ExtensionsAiEmbeddingsOptions.Dimensions " +
            "to the model's dimension size (e.g. 1536 for text-embedding-3-small).");
    }
}

public static class ExtensionsAiEmbeddingsExtensions
{
    /// <summary>
    /// Resolves an <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> from DI and
    /// registers <see cref="ExtensionsAiEmbeddingsProvider"/> as
    /// <see cref="IEmbeddingsProvider"/>. Pair with any provider package that ships
    /// an MEAI generator (OpenAI, Azure OpenAI, Ollama, etc.).
    /// </summary>
    public static BlazorMemoryBuilder UseExtensionsAiEmbeddings(
        this BlazorMemoryBuilder builder,
        Action<ExtensionsAiEmbeddingsOptions>? configure = null)
    {
        var options = new ExtensionsAiEmbeddingsOptions();
        configure?.Invoke(options);
        builder.Services.AddSingleton(options);
        builder.Services.AddScoped<IEmbeddingsProvider>(sp => new ExtensionsAiEmbeddingsProvider(
            sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>(),
            sp.GetRequiredService<ExtensionsAiEmbeddingsOptions>()));
        return builder;
    }

    /// <summary>
    /// Registers <see cref="ExtensionsAiEmbeddingsProvider"/> around a caller-supplied
    /// generator instance. Useful when the generator is not itself in DI.
    /// </summary>
    public static BlazorMemoryBuilder UseExtensionsAiEmbeddings(
        this BlazorMemoryBuilder builder,
        IEmbeddingGenerator<string, Embedding<float>> generator,
        Action<ExtensionsAiEmbeddingsOptions>? configure = null)
    {
        var options = new ExtensionsAiEmbeddingsOptions();
        configure?.Invoke(options);
        builder.Services.AddSingleton(options);
        builder.Services.AddScoped<IEmbeddingsProvider>(_ => new ExtensionsAiEmbeddingsProvider(generator, options));
        return builder;
    }
}
