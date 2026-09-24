using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Engine;
using BlazorMemory.Core.Models;
using BlazorMemory.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

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

    /// <summary>
    /// Registers <see cref="MemoryEnabledChat"/> for injection. It wraps an LLM call with
    /// automatic memory retrieval and extraction so callers don't write the boilerplate.
    /// </summary>
    public BlazorMemoryBuilder UseMemoryEnabledChat()
    {
        Services.AddScoped<MemoryEnabledChat>();
        return this;
    }

    /// <summary>
    /// Registers <see cref="IAgentMemoryServiceFactory"/> so multiple AI agents can share
    /// the same memory store, each writing under their own namespace.
    /// </summary>
    public BlazorMemoryBuilder UseMultiAgentMemory()
    {
        Services.AddScoped<IAgentMemoryServiceFactory, AgentMemoryServiceFactory>();
        return this;
    }

    /// <summary>
    /// Configures extraction dispatch behaviour (mode and inline timeout).
    /// Defaults are <see cref="ExtractionMode.Inline"/> with a 30 second timeout.
    /// </summary>
    public BlazorMemoryBuilder ConfigureExtraction(Action<ExtractionOptions> configure)
    {
        Services.RemoveAll<ExtractionOptions>();
        var options = new ExtractionOptions();
        configure(options);
        Services.AddSingleton(options);
        return this;
    }

    /// <summary>
    /// Switches extraction to <see cref="ExtractionMode.Background"/>, wiring
    /// <see cref="ChannelMemoryExtractionQueue"/> and a <see cref="MemoryExtractionWorker"/>
    /// hosted service. The queue is bounded and drops rather than blocks when full.
    /// <para>
    /// NOTE: Requires a host that runs <see cref="Microsoft.Extensions.Hosting.IHostedService"/>
    /// implementations (ASP.NET Core, worker services, .NET generic host). Blazor
    /// WebAssembly does not run hosted services, so WASM apps must stay on
    /// <see cref="ExtractionMode.Inline"/>.
    /// </para>
    /// </summary>
    public BlazorMemoryBuilder UseBackgroundExtraction(Action<ExtractionOptions>? configure = null)
    {
        Services.RemoveAll<ExtractionOptions>();
        var options = new ExtractionOptions { Mode = ExtractionMode.Background };
        configure?.Invoke(options);
        options.Mode = ExtractionMode.Background;
        Services.AddSingleton(options);
        Services.AddSingleton<IMemoryExtractionQueue, ChannelMemoryExtractionQueue>();
        Services.AddHostedService<MemoryExtractionWorker>();
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
        services.TryAddSingleton<ExtractionOptions>();
        return new BlazorMemoryBuilder(services);
    }
}
