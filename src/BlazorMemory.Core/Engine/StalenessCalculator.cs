using BlazorMemory.Core.Models;

namespace BlazorMemory.Core.Engine;

/// <summary>
/// Calculates a staleness score for a memory based on its age.
/// Score ranges from 0.0 (just learned, perfectly fresh) to 1.0 (very stale).
/// Uses exponential decay: score = 1 - e^(-age / halfLife)
/// </summary>
public static class StalenessCalculator
{
    /// <summary>
    /// Returns a staleness score between 0.0 and 1.0.
    /// </summary>
    /// <param name="entry">The memory entry to score.</param>
    /// <param name="halfLifeDays">
    /// Number of days at which a memory is considered 50% stale. Default 30.
    /// </param>
    /// <param name="now">Current time. Defaults to DateTimeOffset.UtcNow.</param>
    public static float Calculate(
        MemoryEntry entry,
        int halfLifeDays = 30,
        DateTimeOffset? now = null)
    {
        var reference = entry.UpdatedAt ?? entry.LearnedAt;
        var age = ((now ?? DateTimeOffset.UtcNow) - reference).TotalDays;

        if (age <= 0) return 0f;

        // λ = ln(2) / halfLife  →  score = 1 - e^(-λ * age)
        var lambda = Math.Log(2) / halfLifeDays;
        var score = 1.0 - Math.Exp(-lambda * age);

        return (float)Math.Clamp(score, 0.0, 1.0);
    }

    /// <summary>
    /// Applies staleness scores to a list of memory entries.
    /// </summary>
    public static IReadOnlyList<MemoryEntry> ApplyTo(
        IReadOnlyList<MemoryEntry> entries,
        int halfLifeDays = 30,
        DateTimeOffset? now = null)
    {
        var ts = now ?? DateTimeOffset.UtcNow;
        return entries
            .Select(e => e.WithStalenessScore(Calculate(e, halfLifeDays, ts)))
            .ToList();
    }
}
