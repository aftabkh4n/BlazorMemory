using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Engine;
using BlazorMemory.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorMemory.Core.Extensions;

/// <summary>
/// Builder returned by AddBlazorMemory() for fluent adapter registration.
/// </summary>
public sealed class BlazorMemoryBuilder
{
    public IServiceCollection Services { get; }

    internal BlazorMemoryBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>Register a custom storage adapter.</summary>
    public BlazorMemoryBuilder UseStore<TStore>() where TStore : class, IMemoryStore
    {
        Services.AddScoped<IMemoryStore, TStore>();
        return this;
    }

    /// <summary>Register a custom embeddings provider.</summary>
    public BlazorMemoryBuilder UseEmbeddings<TProvider>() where TProvider : class, IEmbeddingsProvider
    {
        Services.AddScoped<IEmbeddingsProvider, TProvider>();
        return this;
    }

    /// <summary>Register a custom extractor.</summary>
    public BlazorMemoryBuilder UseExtractor<TExtractor>() where TExtractor : class, IMemoryExtractor
    {
        Services.AddScoped<IMemoryExtractor, TExtractor>();
        return this;
    }
}

/// <summary>
/// Extension methods for registering BlazorMemory with the .NET DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers BlazorMemory core services. Chain .UseStore(), .UseEmbeddings(), .UseExtractor()
    /// or use the adapter-specific extension methods from adapter packages.
    /// </summary>
    /// <example>
    /// builder.Services
    ///     .AddBlazorMemory()
    ///     .UseIndexedDbStorage()          // from BlazorMemory.Storage.IndexedDb
    ///     .UseOpenAiEmbeddings(apiKey)    // from BlazorMemory.Embeddings.OpenAi
    ///     .UseOpenAiExtractor(apiKey);    // from BlazorMemory.Extractor.OpenAi
    /// </example>
    public static BlazorMemoryBuilder AddBlazorMemory(this IServiceCollection services)
    {
        services.AddScoped<ExtractionEngine>();
        services.AddScoped<IMemoryService, MemoryService>();
        return new BlazorMemoryBuilder(services);
    }
}
