using System.Globalization;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using FluentAssertions;
using Xunit;

namespace BlazorMemory.Storage.Contract.Tests;

// NOTE: Pgvector requires a live PostgreSQL instance and therefore cannot be
//       run in CI without external infrastructure.
//       IndexedDb requires a browser runtime (Blazor WASM) and cannot be unit-tested.
//       Neither adapter is covered by this contract suite.

public abstract class MemoryStoreContractTests
{
    protected abstract Task<IMemoryStore> CreateStoreAsync();

    protected static MemoryEntry MakeEntry(
        string id,
        string userId,
        string content,
        float[]? embedding = null,
        string? @namespace = null,
        float importanceScore = 1.0f,
        Dictionary<string, string>? metadata = null) => new()
    {
        Id              = id,
        UserId          = userId,
        Content         = content,
        Embedding       = embedding ?? [1f, 0f, 0f],
        LearnedAt       = DateTimeOffset.UtcNow,
        Namespace       = @namespace,
        ImportanceScore = importanceScore,
        Metadata        = metadata ?? []
    };

    [Fact]
    public async Task AddAsync_ThenGetAsync_ReturnsSameContent()
    {
        var store = await CreateStoreAsync();
        var entry = MakeEntry("m1", "user1", "Hello world");
        await store.AddAsync(entry);

        var result = await store.GetAsync("m1");

        result.Should().NotBeNull();
        result!.Content.Should().Be("Hello world");
    }

    [Fact]
    public async Task AddAsync_ThenGetAsync_PreservesEmbedding()
    {
        var store = await CreateStoreAsync();
        var embedding = new float[] { 0.1f, 0.25f, -0.75f };
        var entry = MakeEntry("m1", "user1", "test", embedding: embedding);
        await store.AddAsync(entry);

        var result = await store.GetAsync("m1");

        result!.Embedding.Should().HaveCount(3);
        result.Embedding[0].Should().BeApproximately(0.1f, 0.0001f);
        result.Embedding[1].Should().BeApproximately(0.25f, 0.0001f);
        result.Embedding[2].Should().BeApproximately(-0.75f, 0.0001f);
    }

    // Catches FIX 2: ImportanceScore was not persisted in EF Core.
    [Fact]
    public async Task AddAsync_ThenGetAsync_PreservesImportanceScore()
    {
        var store = await CreateStoreAsync();
        var entry = MakeEntry("m1", "user1", "test", importanceScore: 1.5f);
        await store.AddAsync(entry);

        var result = await store.GetAsync("m1");

        result!.ImportanceScore.Should().Be(1.5f);
    }

    // Catches FIX 2: ImportanceScore was not persisted through UpdateAsync in EF Core.
    [Fact]
    public async Task UpdateAsync_PersistsImportanceScore()
    {
        var store = await CreateStoreAsync();
        var entry = MakeEntry("m1", "user1", "test");
        await store.AddAsync(entry);

        await store.UpdateAsync(entry with { ImportanceScore = 0.3f, UpdatedAt = DateTimeOffset.UtcNow });

        var result = await store.GetAsync("m1");
        result!.ImportanceScore.Should().BeApproximately(0.3f, 0.0001f);
    }

    [Fact]
    public async Task AddAsync_ThenGetAsync_PreservesNamespace()
    {
        var store = await CreateStoreAsync();
        var entry = MakeEntry("m1", "user1", "test", @namespace: "ns1");
        await store.AddAsync(entry);

        var result = await store.GetAsync("m1");

        result!.Namespace.Should().Be("ns1");
    }

    [Fact]
    public async Task AddAsync_ThenGetAsync_PreservesMetadata()
    {
        var store = await CreateStoreAsync();
        var entry = MakeEntry("m1", "user1", "test",
            metadata: new Dictionary<string, string> { ["key"] = "value" });
        await store.AddAsync(entry);

        var result = await store.GetAsync("m1");

        result!.Metadata.Should().ContainKey("key").WhoseValue.Should().Be("value");
    }

    [Fact]
    public async Task ListAsync_ScopedToUser()
    {
        var store = await CreateStoreAsync();
        await store.AddAsync(MakeEntry("m1", "user1", "fact A"));
        await store.AddAsync(MakeEntry("m2", "user1", "fact B"));
        await store.AddAsync(MakeEntry("m3", "user2", "fact C"));

        var user1 = await store.ListAsync("user1");
        var user2 = await store.ListAsync("user2");

        user1.Should().HaveCount(2).And.OnlyContain(m => m.UserId == "user1");
        user2.Should().HaveCount(1).And.OnlyContain(m => m.UserId == "user2");
    }

    [Fact]
    public async Task ListAsync_ScopedToNamespace()
    {
        var store = await CreateStoreAsync();
        await store.AddAsync(MakeEntry("m1", "user1", "A", @namespace: "ns1"));
        await store.AddAsync(MakeEntry("m2", "user1", "B", @namespace: "ns2"));
        await store.AddAsync(MakeEntry("m3", "user1", "C"));

        var ns1 = await store.ListAsync("user1", "ns1");

        ns1.Should().HaveCount(1);
        ns1[0].Id.Should().Be("m1");
    }

    [Fact]
    public async Task SearchSimilarAsync_RespectsThreshold()
    {
        var store = await CreateStoreAsync();
        await store.AddAsync(MakeEntry("m1", "user1", "similar",    embedding: [1f, 0f, 0f]));
        await store.AddAsync(MakeEntry("m2", "user1", "dissimilar", embedding: [0f, 1f, 0f]));

        var results = await store.SearchSimilarAsync([1f, 0f, 0f], "user1", 10, 0.9f);

        results.Should().HaveCount(1);
        results[0].Id.Should().Be("m1");
    }

    [Fact]
    public async Task SearchSimilarAsync_OrdersByRelevanceDescending()
    {
        var store = await CreateStoreAsync();
        await store.AddAsync(MakeEntry("m1", "user1", "high", embedding: [1f, 0f, 0f]));
        await store.AddAsync(MakeEntry("m2", "user1", "low",  embedding: [0f, 1f, 0f]));

        var results = await store.SearchSimilarAsync([1f, 0f, 0f], "user1", 10, 0f);

        results.Should().HaveCount(2);
        results[0].Id.Should().Be("m1");
        results[0].RelevanceScore.Should().BeGreaterThan(results[1].RelevanceScore!.Value);
    }

    [Fact]
    public async Task ClearAsync_OnlyClearsTargetUser()
    {
        var store = await CreateStoreAsync();
        await store.AddAsync(MakeEntry("m1", "user1", "fact A"));
        await store.AddAsync(MakeEntry("m2", "user2", "fact B"));

        await store.ClearAsync("user1");

        (await store.ListAsync("user1")).Should().BeEmpty();
        (await store.ListAsync("user2")).Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteAsync_RemovesOnlyTargetEntry()
    {
        var store = await CreateStoreAsync();
        await store.AddAsync(MakeEntry("m1", "user1", "fact A"));
        await store.AddAsync(MakeEntry("m2", "user1", "fact B"));

        await store.DeleteAsync("m1");

        (await store.GetAsync("m1")).Should().BeNull();
        (await store.GetAsync("m2")).Should().NotBeNull();
    }

    // Catches FIX 1: EF Core embedding serialization was culture-dependent.
    // Under a decimal-comma culture (e.g. de-DE), comma-joined floats produce
    // unparseable output. The fix uses semicolons and InvariantCulture.
    [Fact]
    public async Task AddAsync_ThenGetAsync_PreservesEmbedding_UnderCommaDecimalCulture()
    {
        using var culture = new CultureSwitcher("de-DE");

        var store = await CreateStoreAsync();
        var embedding = new float[] { 0.1f, 0.25f, -0.75f, 1.0f };
        var entry = MakeEntry("m1", "user1", "test", embedding: embedding);
        await store.AddAsync(entry);

        var result = await store.GetAsync("m1");

        result!.Embedding.Should().HaveCount(4);
        for (var i = 0; i < embedding.Length; i++)
            result.Embedding[i].Should().BeApproximately(embedding[i], 0.0001f);
    }
}

internal sealed class CultureSwitcher : IDisposable
{
    private readonly CultureInfo _saved;

    public CultureSwitcher(string cultureName)
    {
        _saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(cultureName);
    }

    public void Dispose() => CultureInfo.CurrentCulture = _saved;
}
