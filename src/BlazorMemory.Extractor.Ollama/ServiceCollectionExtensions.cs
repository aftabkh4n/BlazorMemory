using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorMemory.Extractor.Ollama;

public static class OllamaExtractorExtensions
{
    public static BlazorMemoryBuilder UseOllamaExtractor(
        this BlazorMemoryBuilder builder,
        Action<OllamaExtractorOptions>? configure = null)
    {
        if (configure is not null)
            builder.Services.Configure<OllamaExtractorOptions>(configure);

        builder.Services.AddScoped<IMemoryExtractor>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<OllamaExtractorOptions>>().Value;
            var http = new HttpClient { BaseAddress = new Uri(opts.BaseUrl) };
            return new OllamaMemoryExtractor(http, sp.GetRequiredService<IOptions<OllamaExtractorOptions>>());
        });

        return builder;
    }
}
