using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using Microsoft.Extensions.Logging;

namespace BlazorMemory.Core.Services;

/// <summary>
/// Central dispatcher used by <see cref="MemoryEnabledChat"/> and
/// <see cref="MemoryService.ChatWithMemoryAsync"/> so the fire-and-forget
/// hazard is contained in one place.
/// </summary>
internal static class ExtractionDispatcher
{
    public static async Task DispatchAsync(
        ExtractionOptions options,
        IMemoryExtractionQueue? queue,
        IMemoryService memory,
        ILogger logger,
        string conversation,
        string userId,
        string? @namespace,
        CancellationToken callerCt)
    {
        if (options.Mode == ExtractionMode.Background && queue is not null)
        {
            queue.TryEnqueue(new MemoryExtractionWorkItem(conversation, userId, @namespace));
            return;
        }

        if (options.Mode == ExtractionMode.Background && queue is null)
        {
            logger.LogWarning(
                "ExtractionMode.Background configured but no IMemoryExtractionQueue is registered. " +
                "Falling back to Inline. Call builder.UseBackgroundExtraction() from a host that runs IHostedService.");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(callerCt);
        cts.CancelAfter(options.ExtractionTimeout);
        try
        {
            await memory.ExtractAsync(conversation, userId, @namespace, cts.Token)
                        .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!callerCt.IsCancellationRequested)
        {
            logger.LogWarning(
                "Memory extraction timed out after {Timeout}; reply returned without updating memory.",
                options.ExtractionTimeout);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Memory extraction failed after chat.");
        }
    }
}
