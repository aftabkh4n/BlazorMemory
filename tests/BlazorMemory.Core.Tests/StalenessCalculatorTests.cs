using BlazorMemory.Core.Engine;
using BlazorMemory.Core.Models;
using FluentAssertions;

namespace BlazorMemory.Core.Tests;

public class StalenessCalculatorTests
{
    private static MemoryEntry MakeEntry(DateTimeOffset learnedAt) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        UserId = "user_1",
        Content = "Test memory",
        Embedding = [1f, 0f],
        LearnedAt = learnedAt
    };

    [Fact]
    public void Score_IsZero_WhenJustLearned()
    {
        var now = DateTimeOffset.UtcNow;
        var entry = MakeEntry(now);

        var score = StalenessCalculator.Calculate(entry, halfLifeDays: 30, now: now);

        score.Should().BeApproximately(0f, 0.001f);
    }

    [Fact]
    public void Score_IsApproxHalf_AtHalfLife()
    {
        var now = DateTimeOffset.UtcNow;
        var entry = MakeEntry(now.AddDays(-30));

        var score = StalenessCalculator.Calculate(entry, halfLifeDays: 30, now: now);

        // e^(-ln2) ≈ 0.5 → score = 1 - 0.5 = 0.5
        score.Should().BeApproximately(0.5f, 0.01f);
    }

    [Fact]
    public void Score_ApproachesOne_ForVeryOldMemories()
    {
        var now = DateTimeOffset.UtcNow;
        var entry = MakeEntry(now.AddDays(-365));

        var score = StalenessCalculator.Calculate(entry, halfLifeDays: 30, now: now);

        score.Should().BeGreaterThan(0.99f);
    }

    [Fact]
    public void Score_UsesUpdatedAt_WhenAvailable()
    {
        var now = DateTimeOffset.UtcNow;
        // Learned 60 days ago but updated 5 days ago
        var entry = MakeEntry(now.AddDays(-60)) with { UpdatedAt = now.AddDays(-5) };

        var score = StalenessCalculator.Calculate(entry, halfLifeDays: 30, now: now);
        var scoreFromUpdatedAt = StalenessCalculator.Calculate(
            MakeEntry(now.AddDays(-5)), halfLifeDays: 30, now: now);

        score.Should().BeApproximately(scoreFromUpdatedAt, 0.001f);
    }

    [Fact]
    public void ApplyTo_AttachesStalenessScoreToAllEntries()
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new[]
        {
            MakeEntry(now),
            MakeEntry(now.AddDays(-30)),
            MakeEntry(now.AddDays(-90))
        };

        var result = StalenessCalculator.ApplyTo(entries, halfLifeDays: 30, now: now);

        result.Should().OnlyContain(e => e.StalenessScore.HasValue);
        result[0].StalenessScore.Should().BeLessThan(result[1].StalenessScore!.Value);
        result[1].StalenessScore.Should().BeLessThan(result[2].StalenessScore!.Value);
    }
}
