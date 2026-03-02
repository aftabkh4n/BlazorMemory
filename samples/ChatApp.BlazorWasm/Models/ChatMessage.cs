namespace ChatApp.BlazorWasm.Models;

public sealed record UserMessage
{
    public required string Role { get; init; }    // "user" or "assistant"
    public required string Content { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}