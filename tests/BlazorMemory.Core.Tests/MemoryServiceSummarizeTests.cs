using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Engine;
using BlazorMemory.Core.Models;
using BlazorMemory.Core.Services;
using BlazorMemory.Storage.InMemory;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BlazorMemory.Core.Tests;

public class MemoryServiceSummarizeTests
{
    private static float[] FakeEmbedding() =>
        Enumerable.Range(0, 8).Select(i => (float)i / 8).ToArray();

    private (MemoryService service, IMemoryExtractor extractor, InMemoryMemoryStore store) BuildSut()
    {
        var store      = new InMemoryMemoryStore();
        var embeddings = Substitute.For<IEmbeddingsProvider>();
        var extractor  = Substitute.For<IMemoryExtractor>();

        embeddings.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(FakeEmbedding()));

        extractor.SummarizeAsync(Arg.Any<IReadOnlyList<MemoryEntry>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("User background: Test summary."));

        var engine = new ExtractionEngine(store, embeddings, extractor,
            NullLogger<ExtractionEngine>.Instance);
        var service = new MemoryService(store, embeddings, engine,
            NullLogger<MemoryService>.Instance);

        return (service, extractor, store);
    }

    private static MemoryEntry MakeEntry(string id, string userId, DateTimeOffset learnedAt) =>
        new MemoryEntry
        {
            Id        = id,
            UserId    = userId,
            Content   = $"Fact {id}",
            Embedding = FakeEmbedding(),
            LearnedAt = learnedAt,
        };

    [Fact]
    public async Task SummarizeOldMemoriesAsync_DoesNothing_WhenCountBelowThreshold()
    {
        var (service, extractor, store) = BuildSut();

        for (int i = 0; i < 30; i++)
            await store.AddAsync(MakeEntry($"m{i}", "user_1", DateTimeOffset.UtcNow.AddDays(-i)));

        await service.SummarizeOldMemoriesAsync("user_1", maxMemories: 50, keepRecent: 20);

        var all = await store.ListAsync("user_1");
        all.Should().HaveCount(30);
        await extractor.DidNotReceive()
            .SummarizeAsync(Arg.Any<IReadOnlyList<MemoryEntry>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SummarizeOldMemoriesAsync_SummarizesAndDeletesOldMemories_WhenAboveThreshold()
    {
        var (service, extractor, store) = BuildSut();

        for (int i = 0; i < 60; i++)
            await store.AddAsync(MakeEntry($"m{i}", "user_1", DateTimeOffset.UtcNow.AddDays(-i)));

        await service.SummarizeOldMemoriesAsync("user_1", maxMemories: 50, keepRecent: 20);

        // 60 total → 40 oldest deleted + 1 summary added + 20 recent kept = 21
        var all = await store.ListAsync("user_1");
        all.Should().HaveCount(21);
        await extractor.Received(1)
            .SummarizeAsync(Arg.Any<IReadOnlyList<MemoryEntry>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SummarizeOldMemoriesAsync_KeepsMostRecentN_Untouched()
    {
        var (service, _, store) = BuildSut();
        var now = DateTimeOffset.UtcNow;

        for (int i = 0; i < 60; i++)
            await store.AddAsync(MakeEntry($"m{i:D2}", "user_1", now.AddDays(-i)));

        await service.SummarizeOldMemoriesAsync("user_1", maxMemories: 50, keepRecent: 20);

        var remaining = await store.ListAsync("user_1");
        var keptIds   = remaining
            .Where(m => !m.Content.StartsWith("[Summary]"))
            .Select(m => m.Id)
            .ToHashSet();

        // m00..m19 are the 20 most recent (days -0 to -19) and must survive
        for (int i = 0; i < 20; i++)
            keptIds.Should().Contain($"m{i:D2}");

        // m20..m59 (the 40 oldest) must be gone
        for (int i = 20; i < 60; i++)
            keptIds.Should().NotContain($"m{i:D2}");
    }

    [Fact]
    public async Task SummarizeOldMemoriesAsync_StoresSummaryWithPrefix()
    {
        var (service, _, store) = BuildSut();

        for (int i = 0; i < 60; i++)
            await store.AddAsync(MakeEntry($"m{i}", "user_1", DateTimeOffset.UtcNow.AddDays(-i)));

        await service.SummarizeOldMemoriesAsync("user_1", maxMemories: 50, keepRecent: 20);

        var all     = await store.ListAsync("user_1");
        var summary = all.FirstOrDefault(m => m.Content.StartsWith("[Summary]"));
        summary.Should().NotBeNull();
        summary!.Content.Should().Be("[Summary] User background: Test summary.");
    }
}
