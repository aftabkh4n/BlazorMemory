using BlazorMemory.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorMemory.AgentFramework;

/// <summary>
/// DI helpers to attach a <see cref="BlazorMemoryContextProvider"/> to a Microsoft
/// Agent Framework <c>AIAgent</c>. Consumers resolve the provider from DI and pass it
/// through <c>ChatClientAgentOptions.AIContextProviders</c> (or the equivalent
/// <c>AIAgentBuilder.UseAIContextProviders</c> / <c>ChatClientBuilder.UseAIContextProviders</c>
/// pattern) at agent construction time.
/// </summary>
public static class AgentFrameworkExtensions
{
    /// <summary>
    /// Registers <see cref="BlazorMemoryContextProvider"/> as a scoped service so it can
    /// be handed to an <c>AIAgent</c> at construction. Configure a
    /// <see cref="BlazorMemoryContextProviderOptions.UserIdResolver"/> — the constructor
    /// throws without one.
    /// </summary>
    public static BlazorMemoryBuilder UseAgentFrameworkMemory(
        this BlazorMemoryBuilder builder,
        Action<BlazorMemoryContextProviderOptions> configure)
    {
        var options = new BlazorMemoryContextProviderOptions();
        configure(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddScoped<BlazorMemoryContextProvider>();
        return builder;
    }
}
