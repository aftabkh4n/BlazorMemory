using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using SKIMemoryStore = Microsoft.SemanticKernel.Memory.IMemoryStore;

namespace BlazorMemory.SemanticKernel;

public static class SemanticKernelExtensions
{
    /// <summary>
    /// Registers <see cref="BlazorMemoryMemoryStore"/> as Semantic Kernel's
    /// <c>IMemoryStore</c> in the DI container.
    /// </summary>
    /// <param name="userId">
    /// The BlazorMemory user ID used to scope all memory operations.
    /// Defaults to <c>"sk"</c>.
    /// </param>
    [Obsolete("Semantic Kernel memory is superseded by Microsoft Agent Framework. Use BlazorMemory.AgentFramework.")]
    public static BlazorMemoryBuilder UseSemanticKernelMemoryStore(
        this BlazorMemoryBuilder builder,
        string userId = "sk")
    {
#pragma warning disable CS0618 // Type is obsolete; this is the legacy adapter itself
        builder.Services.AddScoped<SKIMemoryStore>(sp =>
            new BlazorMemoryMemoryStore(sp.GetRequiredService<IMemoryStore>(), userId));
#pragma warning restore CS0618
        return builder;
    }
}
