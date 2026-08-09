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

public class VerbatimImportanceTests
{
    private static MemoryService BuildSut(IVerbatimStore verbatimStore)
    {
        var store      = Substitute.For<IMemoryStore>();
        var embeddings = Substitute.For<IEmbeddingsProvider>();
        var extractor  = Substitute.For<IMemoryExtractor>();
        var engine     = new ExtractionEngine(store, embeddings, extractor,
            NullLogger<ExtractionEngine>.Instance);
        return new MemoryService(store, embeddings, engine,
            NullLogger<MemoryService>.Instance, verbatimStore);
    }

    private static async Task<VerbatimMemory> SeedAsync(InMemoryVerbatimStore store, string userId, string content)
    {
        var memory = new VerbatimMemory
        {
            Id        = Guid.NewGuid().ToString("N"),
            UserId    = userId,
            Content   = content,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await store.StoreAsync(memory);
        return memory;
    }

    [Fact]
    public async Task MarkVerbatimImportantAsync_SetsScoreToImportant()
    {
        var verbatimStore = new InMemoryVerbatimStore();
        var sut           = BuildSut(verbatimStore);
        var memory        = await SeedAsync(verbatimStore, "u1", "some verbatim content");

        await sut.MarkVerbatimImportantAsync(memory.Id);

        var results = await verbatimStore.GetRecentAsync("u1");
        results.Should().ContainSingle(m => m.Id == memory.Id)
            .Which.ImportanceScore.Should().Be(ImportanceLevels.Important);
    }

    [Fact]
    public async Task MarkVerbatimUnimportantAsync_SetsScoreToUnimportant()
    {
        var verbatimStore = new InMemoryVerbatimStore();
        var sut           = BuildSut(verbatimStore);
        var memory        = await SeedAsync(verbatimStore, "u1", "some verbatim content");

        await sut.MarkVerbatimUnimportantAsync(memory.Id);

        var results = await verbatimStore.GetRecentAsync("u1");
        results.Should().ContainSingle(m => m.Id == memory.Id)
            .Which.ImportanceScore.Should().Be(ImportanceLevels.Unimportant);
    }

    [Fact]
    public async Task ResetVerbatimImportanceAsync_RestoresNeutralScore()
    {
        var verbatimStore = new InMemoryVerbatimStore();
        var sut           = BuildSut(verbatimStore);
        var memory        = await SeedAsync(verbatimStore, "u1", "some verbatim content");

        await sut.MarkVerbatimImportantAsync(memory.Id);
        await sut.ResetVerbatimImportanceAsync(memory.Id);

        var results = await verbatimStore.GetRecentAsync("u1");
        results.Should().ContainSingle(m => m.Id == memory.Id)
            .Which.ImportanceScore.Should().Be(ImportanceLevels.Neutral);
    }

    [Fact]
    public async Task UpdateImportanceAsync_DoesNotAffectOtherEntries()
    {
        var verbatimStore = new InMemoryVerbatimStore();
        var sut           = BuildSut(verbatimStore);
        var target        = await SeedAsync(verbatimStore, "u1", "target entry");
        var bystander     = await SeedAsync(verbatimStore, "u1", "bystander entry");

        await sut.MarkVerbatimImportantAsync(target.Id);

        var results      = await verbatimStore.GetRecentAsync("u1");
        var bystanderRow = results.Single(m => m.Id == bystander.Id);
        bystanderRow.ImportanceScore.Should().Be(ImportanceLevels.Neutral);
    }

    [Fact]
    public async Task MarkVerbatimImportantAsync_Throws_WhenNoVerbatimStoreRegistered()
    {
        var sut = BuildSut(verbatimStore: null!);

        var act = async () => await sut.MarkVerbatimImportantAsync("any-id");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
