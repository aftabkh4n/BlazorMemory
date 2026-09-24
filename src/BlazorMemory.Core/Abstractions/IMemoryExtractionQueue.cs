namespace BlazorMemory.Core.Abstractions;

/// <summary>
/// A single conversation queued for background memory extraction.
/// </summary>
public sealed record MemoryExtractionWorkItem(
    string Conversation,
    string UserId,
    string? Namespace);

/// <summary>
/// Bounded queue for background extraction. Implementations must never block
/// the producer; if the queue is full, <see cref="TryEnqueue"/> returns false
/// and the caller is expected to log and continue.
/// </summary>
public interface IMemoryExtractionQueue
{
    /// <summary>
    /// Enqueues a work item. Returns false if the queue is full (item dropped).
    /// </summary>
    bool TryEnqueue(MemoryExtractionWorkItem item);

    /// <summary>
    /// Async stream of queued items for the background worker to drain.
    /// Completes when the queue is closed or <paramref name="ct"/> is cancelled.
    /// </summary>
    IAsyncEnumerable<MemoryExtractionWorkItem> DequeueAllAsync(CancellationToken ct);
}
