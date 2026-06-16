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

    public string? Namespace { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];

    /// <summary>
    /// Importance score from user feedback. 1.0 = neutral (default),
    /// 1.5 = marked important (thumbs up),
    /// 0.3 = marked unimportant (thumbs down).
    /// Used as a multiplier on cosine similarity during search.
    /// </summary>
    public float ImportanceScore { get; init; } = 1.0f;

    public float? StalenessScore { get; init; }
    public float? RelevanceScore { get; init; }

    public MemoryEntry WithStalenessScore(float score) => this with { StalenessScore = score };
    public MemoryEntry WithRelevanceScore(float score) => this with { RelevanceScore = score };
    public MemoryEntry WithImportanceScore(float score) => this with { ImportanceScore = score };
}

/// <summary>
/// Standard importance values for user feedback.
/// </summary>
public static class ImportanceLevels
{
    public const float Important   = 1.5f;
    public const float Neutral     = 1.0f;
    public const float Unimportant = 0.3f;
}

public sealed record QueryOptions
{
    public int   Limit     { get; init; } = 5;
    public float Threshold { get; init; } = 0.7f;
    public int? MaxAgeInDays { get; init; }
    public bool IncludeStalenessScore  { get; init; } = false;
    public int  StalenessHalfLifeDays  { get; init; } = 30;
    public string? Namespace { get; init; }

    /// <summary>
    /// When true, ImportanceScore is applied as a multiplier on the relevance score.
    /// Default true.
    /// </summary>
    public bool ApplyImportanceScore { get; init; } = true;
}

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