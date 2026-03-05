using Xunit;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Engine;
using BlazorMemory.Core.Models;
using BlazorMemory.Core.Services;
using BlazorMemory.Storage.InMemory;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BlazorMemory.Core.Tests;

public class InMemoryStoreTests
{
    private static float[] FakeEmbedding() => Enumerable.Range(0, 8).Select(i => (float)i / 8).ToArray();

    private (MemoryService service, IMemoryExtractor extractor, InMemoryMemoryStore store) BuildSut()
    {
        var store = new InMemoryMemoryStore();
        var embeddings = Substitute.For<IEmbeddingsProvider>();
        var extractor = Substitute.For<IMemoryExtractor>();

        embeddings.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(FakeEmbedding()));
        embeddings.EmbedBatchAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<float[]>>(
                ci.Arg<IEnumerable<string>>().Select(_ => FakeEmbedding()).ToList()));

        var engine = new ExtractionEngine(store, embeddings, extractor,
            NullLogger<ExtractionEngine>.Instance);

        var service = new MemoryService(store, embeddings, engine,
            NullLogger<MemoryService>.Instance);

        return (service, extractor, store);
    }

    [Fact]
    public async Task ExtractAsync_StoresNewFact_WhenDecisionIsAdd()
    {
        var (service, extractor, store) = BuildSut();

        extractor.ExtractFactsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["User is a software engineer."]));
        extractor.ConsolidateAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<MemoryEntry>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ConsolidationDecision.Add()));

        await service.ExtractAsync("User: I am a software engineer.", "user_1");

        var memories = await store.ListAsync("user_1");
        memories.Should().HaveCount(1);
        memories[0].Content.Should().Be("User is a software engineer.");
    }

    [Fact]
    public async Task ExtractAsync_DoesNotStoreDuplicate_WhenDecisionIsNone()
    {
        var (service, extractor, store) = BuildSut();

        extractor.ExtractFactsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["User is a software engineer."]));
        extractor.ConsolidateAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<MemoryEntry>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ConsolidationDecision.None()));

        await service.ExtractAsync("User: I am a software engineer.", "user_1");

        var memories = await store.ListAsync("user_1");
        memories.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_FiltersOutOldMemories_WhenMaxAgeSet()
    {
        var (service, _, store) = BuildSut();

        await store.AddAsync(new MemoryEntry
        {
            Id = "old",
            UserId = "user_1",
            Content = "Old fact",
            Embedding = FakeEmbedding(),
            LearnedAt = DateTimeOffset.UtcNow.AddDays(-100)
        });
        await store.AddAsync(new MemoryEntry
        {
            Id = "recent",
            UserId = "user_1",
            Content = "Recent fact",
            Embedding = FakeEmbedding(),
            LearnedAt = DateTimeOffset.UtcNow.AddDays(-5)
        });

        var results = await service.QueryAsync("anything", "user_1",
            new QueryOptions { Threshold = 0f, MaxAgeInDays = 30 });

        results.Should().HaveCount(1);
        results[0].Id.Should().Be("recent");
    }

    [Fact]
    public async Task QueryAsync_IncludesStalenessScore_WhenRequested()
    {
        var (service, _, store) = BuildSut();

        await store.AddAsync(new MemoryEntry
        {
            Id = "mem_1",
            UserId = "user_1",
            Content = "Some fact",
            Embedding = FakeEmbedding(),
            LearnedAt = DateTimeOffset.UtcNow.AddDays(-30)
        });

        var results = await service.QueryAsync("anything", "user_1",
            new QueryOptions { Threshold = 0f, IncludeStalenessScore = true });

        results.Should().HaveCount(1);
        results[0].StalenessScore.Should().NotBeNull();
        results[0].StalenessScore.Should().BeApproximately(0.5f, 0.05f);
    }

    [Fact]
    public async Task UpdateAsync_ChangesContent()
    {
        var (service, _, store) = BuildSut();

        var entry = new MemoryEntry
        {
            Id = "mem_1",
            UserId = "user_1",
            Content = "Original",
            Embedding = FakeEmbedding(),
            LearnedAt = DateTimeOffset.UtcNow
        };
        await store.AddAsync(entry);

        await service.UpdateAsync("mem_1", "Updated content");

        var result = await service.GetAsync("mem_1");
        result!.Content.Should().Be("Updated content");
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        var (service, _, store) = BuildSut();

        var entry = new MemoryEntry
        {
            Id = "mem_1",
            UserId = "user_1",
            Content = "To delete",
            Embedding = FakeEmbedding(),
            LearnedAt = DateTimeOffset.UtcNow
        };
        await store.AddAsync(entry);

        await service.DeleteAsync("mem_1");

        var result = await service.GetAsync("mem_1");
        result.Should().BeNull();
    }
}