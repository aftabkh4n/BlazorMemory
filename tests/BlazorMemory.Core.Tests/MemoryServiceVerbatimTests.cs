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

public class MemoryServiceVerbatimTests
{
    private static float[] FakeEmbedding() => [0.1f, 0.2f, 0.3f];

    private static MemoryService BuildSut(IVerbatimStore? verbatimStore = null)
    {
        var store      = Substitute.For<IMemoryStore>();
        var embeddings = Substitute.For<IEmbeddingsProvider>();
        var extractor  = Substitute.For<IMemoryExtractor>();

        embeddings.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(FakeEmbedding());
        embeddings.Dimensions.Returns(3);

        var engine = new ExtractionEngine(store, embeddings, extractor,
            NullLogger<ExtractionEngine>.Instance);

        return new MemoryService(store, embeddings, engine,
            NullLogger<MemoryService>.Instance,
            verbatimStore);
    }

    [Fact]
    public async Task StoreVerbatimAsync_PersistsContent()
    {
        var verbatimStore = new InMemoryVerbatimStore();
        var sut           = BuildSut(verbatimStore);

        await sut.StoreVerbatimAsync("u1", "some verbatim text");

        var results = await verbatimStore.GetRecentAsync("u1");
        results.Should().ContainSingle(m => m.Content == "some verbatim text" && m.UserId == "u1");
    }

    [Fact]
    public async Task StoreVerbatimAsync_GeneratesUniqueId()
    {
        var verbatimStore = new InMemoryVerbatimStore();
        var sut           = BuildSut(verbatimStore);

        await sut.StoreVerbatimAsync("u1", "first");
        await sut.StoreVerbatimAsync("u1", "second");

        var results = await verbatimStore.GetRecentAsync("u1");
        results.Should().HaveCount(2);
        results.Select(m => m.Id).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task StoreVerbatimAsync_StoresMetadata()
    {
        var verbatimStore = new InMemoryVerbatimStore();
        var sut           = BuildSut(verbatimStore);
        var meta          = new Dictionary<string, string> { ["source"] = "chat" };

        await sut.StoreVerbatimAsync("u1", "with metadata", meta);

        var results = await verbatimStore.GetRecentAsync("u1");
        results[0].Metadata.Should().ContainKey("source").WhoseValue.Should().Be("chat");
    }

    [Fact]
    public async Task StoreVerbatimAsync_Throws_WhenNoVerbatimStore()
    {
        var sut = BuildSut(verbatimStore: null);

        var act = () => sut.StoreVerbatimAsync("u1", "content");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StoreVerbatimAsync_Throws_OnBlankUserId(string userId)
    {
        var sut = BuildSut(new InMemoryVerbatimStore());

        var act = () => sut.StoreVerbatimAsync(userId, "content");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StoreVerbatimAsync_Throws_OnBlankContent(string content)
    {
        var sut = BuildSut(new InMemoryVerbatimStore());

        var act = () => sut.StoreVerbatimAsync("u1", content);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SearchVerbatimAsync_WithQuery_ReturnsMatchingEntries()
    {
        var verbatimStore = new InMemoryVerbatimStore();
        var sut           = BuildSut(verbatimStore);

        await sut.StoreVerbatimAsync("u1", "The quick brown fox");
        await sut.StoreVerbatimAsync("u1", "unrelated content");

        var results = await sut.SearchVerbatimAsync("u1", "quick brown");

        results.Should().ContainSingle(m => m.Content == "The quick brown fox");
    }

    [Fact]
    public async Task SearchVerbatimAsync_WithEmptyQuery_ReturnsRecentEntries()
    {
        var verbatimStore = new InMemoryVerbatimStore();
        var sut           = BuildSut(verbatimStore);
        var now           = DateTimeOffset.UtcNow;

        await sut.StoreVerbatimAsync("u1", "first");
        await sut.StoreVerbatimAsync("u1", "second");

        var results = await sut.SearchVerbatimAsync("u1", "");

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchVerbatimAsync_WithWhitespaceQuery_ReturnsRecentEntries()
    {
        var verbatimStore = new InMemoryVerbatimStore();
        var sut           = BuildSut(verbatimStore);

        await sut.StoreVerbatimAsync("u1", "some fact");

        var results = await sut.SearchVerbatimAsync("u1", "   ");

        results.Should().ContainSingle();
    }

    [Fact]
    public async Task SearchVerbatimAsync_WithZeroLimit_ReturnsEmpty()
    {
        var verbatimStore = new InMemoryVerbatimStore();
        var sut           = BuildSut(verbatimStore);
        await sut.StoreVerbatimAsync("u1", "content");

        var results = await sut.SearchVerbatimAsync("u1", "content", limit: 0);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteVerbatimAsync_RemovesEntry()
    {
        var verbatimStore = new InMemoryVerbatimStore();
        var sut           = BuildSut(verbatimStore);

        await sut.StoreVerbatimAsync("u1", "to delete");
        var stored = (await verbatimStore.GetRecentAsync("u1"))[0];

        await sut.DeleteVerbatimAsync(stored.Id);

        var results = await verbatimStore.GetRecentAsync("u1");
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearVerbatimAsync_RemovesAllForUser()
    {
        var verbatimStore = new InMemoryVerbatimStore();
        var sut           = BuildSut(verbatimStore);

        await sut.StoreVerbatimAsync("u1", "fact 1");
        await sut.StoreVerbatimAsync("u1", "fact 2");

        await sut.ClearVerbatimAsync("u1");

        var results = await verbatimStore.GetRecentAsync("u1");
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExportVerbatimAsync_ReturnsValidJson()
    {
        var verbatimStore = new InMemoryVerbatimStore();
        var sut           = BuildSut(verbatimStore);

        await sut.StoreVerbatimAsync("u1", "exportable fact");

        var json = await sut.ExportVerbatimAsync("u1");

        json.Should().Contain("exportable fact");
        json.Should().Contain("\"userId\"");
        json.Should().Contain("\"memories\"");
    }

    [Fact]
    public async Task ImportVerbatimAsync_SkipsDuplicates()
    {
        var verbatimStore = new InMemoryVerbatimStore();
        var sut           = BuildSut(verbatimStore);

        await sut.StoreVerbatimAsync("u1", "existing fact");
        var json = await sut.ExportVerbatimAsync("u1");

        await sut.ImportVerbatimAsync("u1", json);

        var results = await verbatimStore.GetRecentAsync("u1");
        results.Should().ContainSingle();
    }

    [Fact]
    public async Task ImportVerbatimAsync_AddsNewMemories()
    {
        var verbatimStore = new InMemoryVerbatimStore();
        var sut           = BuildSut(verbatimStore);

        await sut.StoreVerbatimAsync("export_user", "fact A");
        await sut.StoreVerbatimAsync("export_user", "fact B");
        var json = await sut.ExportVerbatimAsync("export_user");

        await sut.ImportVerbatimAsync("import_user", json);

        var results = await verbatimStore.GetRecentAsync("import_user");
        results.Should().HaveCount(2);
    }
}
