using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorMemory.Embeddings.Ollama;

public static class OllamaEmbeddingsExtensions
{
    /// <summary>
    /// Registers Ollama as the embeddings provider. Defaults to nomic-embed-text at http://localhost:11434.
    /// </summary>
    public static BlazorMemoryBuilder UseOllamaEmbeddings(
        this BlazorMemoryBuilder builder,
        Action<OllamaEmbeddingsOptions>? configure = null)
    {
        if (configure is not null)
            builder.Services.Configure<OllamaEmbeddingsOptions>(configure);

        builder.Services.AddScoped<IEmbeddingsProvider>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<OllamaEmbeddingsOptions>>().Value;
            var http = new HttpClient { BaseAddress = new Uri(opts.BaseUrl) };
            return new OllamaEmbeddingsProvider(http, sp.GetRequiredService<IOptions<OllamaEmbeddingsOptions>>());
        });

        return builder;
    }
}
