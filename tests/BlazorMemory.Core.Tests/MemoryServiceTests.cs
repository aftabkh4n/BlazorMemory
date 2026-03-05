using Xunit;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using BlazorMemory.Core.Services;
using BlazorMemory.Storage.InMemory;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BlazorMemory.Core.Tests;

public class MemoryServiceTests
{
    private static float[] FakeEmbedding() => [0.1f, 0.2f, 0.3f];

    private static (MemoryService sut, IMemoryStore store, IEmbeddingsProvider embeddings, IMemoryExtractor extractor)
        BuildSut()
    {
        var store = Substitute.For<IMemoryStore>();
        var embeddings = Substitute.For<IEmbeddingsProvider>();
        var extractor = Substitute.For<IMemoryExtractor>();

        embeddings.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(FakeEmbedding());
        embeddings.Dimensions.Returns(3);

        store.SearchSimilarAsync(
                Arg.Any<float[]>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<float>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MemoryEntry>());

        extractor.ExtractFactsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "User is a developer" });
        extractor.ConsolidateAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<MemoryEntry>>(),
                Arg.Any<CancellationToken>())
            .Returns(ConsolidationDecision.Add());

        // MemoryService requires ExtractionEngine + ILogger - build them properly
        var inMemoryStore = new InMemoryMemoryStore();
        var engine = new BlazorMemory.Core.Engine.ExtractionEngine(
                                 store, embeddings, extractor,
                                 NullLogger<BlazorMemory.Core.Engine.ExtractionEngine>.Instance);
        var sut = new MemoryService(store, embeddings, engine,
                      NullLogger<MemoryService>.Instance);

        return (sut, store, embeddings, extractor);
    }

    [Fact]
    public async Task ExtractAsync_StoresNewFact_WhenDecisionIsAdd()
    {
        var (sut, store, _, _) = BuildSut();

        await sut.ExtractAsync("User: I am a developer", "user_1");

        await store.Received(1).AddAsync(
            Arg.Is<MemoryEntry>(m => m.Content == "User is a developer"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExtractAsync_DoesNotStoreDuplicate_WhenDecisionIsNone()
    {
        var (sut, store, _, extractor) = BuildSut();

        extractor.ConsolidateAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<MemoryEntry>>(),
                Arg.Any<CancellationToken>())
            .Returns(ConsolidationDecision.None());

        await sut.ExtractAsync("Some conversation", "user_1");

        await store.DidNotReceive().AddAsync(
            Arg.Any<MemoryEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_FiltersOutOldMemories_WhenMaxAgeSet()
    {
        var (sut, store, _, _) = BuildSut();

        var oldMemory = new MemoryEntry
        {
            Id = "old",
            UserId = "user_1",
            Content = "old fact",
            Embedding = FakeEmbedding(),
            LearnedAt = DateTimeOffset.UtcNow.AddDays(-200)
        };
        var newMemory = new MemoryEntry
        {
            Id = "new",
            UserId = "user_1",
            Content = "new fact",
            Embedding = FakeEmbedding(),
            LearnedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };

        store.SearchSimilarAsync(
                Arg.Any<float[]>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<float>(), Arg.Any<CancellationToken>())
            .Returns(new List<MemoryEntry> { oldMemory, newMemory });

        var results = await sut.QueryAsync("test", "user_1",
            new QueryOptions { MaxAgeInDays = 90 });

        results.Should().HaveCount(1);
        results[0].Id.Should().Be("new");
    }

    [Fact]
    public async Task QueryAsync_IncludesStalenessScore_WhenRequested()
    {
        var (sut, store, _, _) = BuildSut();

        var memory = new MemoryEntry
        {
            Id = "m1",
            UserId = "user_1",
            Content = "some fact",
            Embedding = FakeEmbedding(),
            LearnedAt = DateTimeOffset.UtcNow.AddDays(-30)
        };

        store.SearchSimilarAsync(
                Arg.Any<float[]>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<float>(), Arg.Any<CancellationToken>())
            .Returns(new List<MemoryEntry> { memory });

        var results = await sut.QueryAsync("test", "user_1",
            new QueryOptions { IncludeStalenessScore = true, MaxAgeInDays = 365 });

        results.Should().HaveCount(1);
        results[0].StalenessScore.Should().BeGreaterThan(0f);
    }

    [Fact]
    public async Task UpdateAsync_ChangesContent()
    {
        var (sut, store, _, _) = BuildSut();

        var existing = new MemoryEntry
        {
            Id = "m1",
            UserId = "user_1",
            Content = "original",
            Embedding = FakeEmbedding(),
            LearnedAt = DateTimeOffset.UtcNow
        };

        store.GetAsync("m1", Arg.Any<CancellationToken>()).Returns(existing);

        await sut.UpdateAsync("m1", "updated content");

        await store.Received(1).UpdateAsync(
            Arg.Is<MemoryEntry>(m => m.Content == "updated content"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        var (sut, store, _, _) = BuildSut();

        await sut.DeleteAsync("m1");

        await store.Received(1).DeleteAsync("m1", Arg.Any<CancellationToken>());
    }
}