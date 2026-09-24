using System.Threading.Channels;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using Microsoft.Extensions.Logging;

namespace BlazorMemory.Core.Services;

/// <summary>
/// Bounded <see cref="System.Threading.Channels.Channel"/>-backed queue. When
/// the channel is full, new items are dropped and logged rather than blocking
/// the producing chat request.
/// </summary>
public sealed class ChannelMemoryExtractionQueue : IMemoryExtractionQueue
{
    private readonly Channel<MemoryExtractionWorkItem>      _channel;
    private readonly ILogger<ChannelMemoryExtractionQueue>  _logger;

    public ChannelMemoryExtractionQueue(
        ExtractionOptions options,
        ILogger<ChannelMemoryExtractionQueue> logger)
    {
        var capacity = options.BackgroundQueueCapacity < 1 ? 1 : options.BackgroundQueueCapacity;
        // FullMode = Wait so TryWrite returns false when full; we translate that into a
        // logged drop rather than a blocked producer, since chat requests must not wait.
        _channel = Channel.CreateBounded<MemoryExtractionWorkItem>(
            new BoundedChannelOptions(capacity)
            {
                FullMode     = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
        _logger = logger;
    }

    public bool TryEnqueue(MemoryExtractionWorkItem item)
    {
        if (_channel.Writer.TryWrite(item))
            return true;

        _logger.LogWarning(
            "Memory extraction queue full (capacity reached); dropping item for user {UserId}.",
            item.UserId);
        return false;
    }

    public IAsyncEnumerable<MemoryExtractionWorkItem> DequeueAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
