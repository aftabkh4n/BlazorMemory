using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorMemory.Embeddings.AzureOpenAi;

public static class AzureOpenAiEmbeddingsExtensions
{
    public static BlazorMemoryBuilder UseAzureOpenAiEmbeddings(
        this BlazorMemoryBuilder builder,
        Action<AzureOpenAiEmbeddingsOptions>? configure = null)
    {
        if (configure is not null)
            builder.Services.Configure<AzureOpenAiEmbeddingsOptions>(configure);

        builder.Services.AddScoped<IEmbeddingsProvider>(sp =>
            new AzureOpenAiEmbeddingsProvider(
                new HttpClient(),
                sp.GetRequiredService<IOptions<AzureOpenAiEmbeddingsOptions>>()));

        return builder;
    }
}
