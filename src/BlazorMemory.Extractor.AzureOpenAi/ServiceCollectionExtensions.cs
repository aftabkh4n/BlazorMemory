using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorMemory.Extractor.AzureOpenAi;

public static class AzureOpenAiExtractorExtensions
{
    public static BlazorMemoryBuilder UseAzureOpenAiExtractor(
        this BlazorMemoryBuilder builder,
        Action<AzureOpenAiExtractorOptions>? configure = null)
    {
        if (configure is not null)
            builder.Services.Configure<AzureOpenAiExtractorOptions>(configure);

        builder.Services.AddScoped<IMemoryExtractor>(sp =>
            new AzureOpenAiMemoryExtractor(
                new HttpClient(),
                sp.GetRequiredService<IOptions<AzureOpenAiExtractorOptions>>()));

        return builder;
    }
}
