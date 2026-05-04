namespace BlazorMemory.Core.Models;

/// <summary>
/// Root object for a memory export file.
/// </summary>
public sealed class MemoryExport
{
    public string            UserId     { get; set; } = string.Empty;
    public string?           Namespace  { get; set; }
    public DateTimeOffset    ExportedAt { get; set; }
    public string            Version    { get; set; } = "1.0";
    public List<MemoryExportEntry> Memories { get; set; } = [];
}

/// <summary>
/// A single memory entry in an export file.
/// Embedding is intentionally excluded — it is re-generated on import.
/// </summary>
public sealed class MemoryExportEntry
{
    public string                       Id        { get; set; } = string.Empty;
    public string                       Content   { get; set; } = string.Empty;
    public string?                      Namespace { get; set; }
    public DateTimeOffset               LearnedAt { get; set; }
    public DateTimeOffset?              UpdatedAt { get; set; }
    public Dictionary<string, string>?  Metadata  { get; set; }
}