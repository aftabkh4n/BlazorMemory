namespace BlazorMemory.Core.Models;

/// <summary>
/// A single stored memory entry.
/// </summary>
public sealed record MemoryEntry
{
    public required string Id        { get; init; }
    public required string UserId    { get; init; }
    public required string Content   { get; init; }
    public required float[] Embedding { get; init; }
    public required DateTimeOffset LearnedAt { get; init; }

    /// <summary>
    /// Optional namespace for segmenting memories by topic or context.
    /// Examples: "work", "personal", "project_alpha".
    /// Null means the default/global namespace.
    /// </summary>
    public string? Namespace { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];

    /// <summary>
    /// Staleness score from 0.0 (fresh) to 1.0 (very stale).
    /// Only populated when QueryOptions.IncludeStalenessScore = true.
    /// </summary>
    public float? StalenessScore { get; init; }

    /// <summary>
    /// Cosine similarity score from the last vector search (0.0–1.0).
    /// Only populated on query results.
    /// </summary>
    public float? RelevanceScore { get; init; }

    public MemoryEntry WithStalenessScore(float score) => this with { StalenessScore = score };
    public MemoryEntry WithRelevanceScore(float score) => this with { RelevanceScore = score };
}

/// <summary>
/// Options for querying memories.
/// </summary>
public sealed record QueryOptions
{
    public int   Limit     { get; init; } = 5;
    public float Threshold { get; init; } = 0.7f;

    /// <summary>Ignore memories older than N days. Null = no age limit.</summary>
    public int? MaxAgeInDays { get; init; }

    /// <summary>
    /// When true, a StalenessScore is calculated and attached to each result.
    /// </summary>
    public bool IncludeStalenessScore  { get; init; } = false;
    public int  StalenessHalfLifeDays  { get; init; } = 30;

    /// <summary>
    /// Filter results to a specific namespace.
    /// Null returns memories from all namespaces.
    /// </summary>
    public string? Namespace { get; init; }
}

/// <summary>
/// The result of a consolidation decision returned by the LLM extractor.
/// </summary>
public sealed record ConsolidationDecision
{
    public required ConsolidationAction Action { get; init; }
    public string? UpdatedContent  { get; init; }
    public string? TargetMemoryId  { get; init; }

    public static ConsolidationDecision Add()    => new() { Action = ConsolidationAction.Add };
    public static ConsolidationDecision None()   => new() { Action = ConsolidationAction.None };
    public static ConsolidationDecision Update(string targetId, string updatedContent) =>
        new() { Action = ConsolidationAction.Update, TargetMemoryId = targetId, UpdatedContent = updatedContent };
    public static ConsolidationDecision Delete(string targetId) =>
        new() { Action = ConsolidationAction.Delete, TargetMemoryId = targetId };
}

public enum ConsolidationAction { Add, Update, Delete, None }