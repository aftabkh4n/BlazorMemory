namespace BlazorMemory.Core.Models;

public class VerbatimMemory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string UserId { get; set; } = default!;

    public string Content { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Dictionary<string, string>? Metadata { get; set; }
}