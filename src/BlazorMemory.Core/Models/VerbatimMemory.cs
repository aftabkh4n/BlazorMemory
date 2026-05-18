namespace BlazorMemory.Core.Models;

/// <summary>
/// A raw memory captured exactly as supplied by the caller.
/// </summary>
public sealed record VerbatimMemory
{
    public required string Id { get; init; }

    public required string UserId { get; init; }

    public required string Content { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public Dictionary<string, string> Metadata { get; init; } = [];
}
