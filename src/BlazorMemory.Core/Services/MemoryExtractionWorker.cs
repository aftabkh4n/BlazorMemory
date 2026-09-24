using BlazorMemory.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorMemory.Core.Services;

/// <summary>
/// Hosted service that drains <see cref="IMemoryExtractionQueue"/> in a single
/// long-running loop. Each item runs in its own DI scope so scoped stores and
/// extractors are resolved correctly per work unit.
/// </summary>
public sealed class MemoryExtractionWorker : BackgroundService
{
    private readonly IMemoryExtractionQueue           _queue;
    private readonly IServiceScopeFactory             _scopeFactory;
    private readonly ILogger<MemoryExtractionWorker>  _logger;

    public MemoryExtractionWorker(
        IMemoryExtractionQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<MemoryExtractionWorker> logger)
    {
        _queue        = queue;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _queue.DequeueAllAsync(stoppingToken).ConfigureAwait(false))
        {
            if (stoppingToken.IsCancellationRequested) break;
            await RunOneAsync(item, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunOneAsync(MemoryExtractionWorkItem item, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var memory = scope.ServiceProvider.GetRequiredService<IMemoryService>();
            await memory.ExtractAsync(item.Conversation, item.UserId, item.Namespace, ct)
                        .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host is shutting down. Not an error.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Background memory extraction failed for user {UserId}.", item.UserId);
        }
    }
}
