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
    public static BlazorMemoryBuilder UseSemanticKernelMemoryStore(
        this BlazorMemoryBuilder builder,
        string userId = "sk")
    {
        builder.Services.AddScoped<SKIMemoryStore>(sp =>
            new BlazorMemoryMemoryStore(sp.GetRequiredService<IMemoryStore>(), userId));
        return builder;
    }
}
