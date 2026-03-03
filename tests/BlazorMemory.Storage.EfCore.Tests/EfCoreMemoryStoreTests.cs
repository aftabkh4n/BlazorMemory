using BlazorMemory.Core.Models;
using BlazorMemory.Storage.EfCore;
using BlazorMemory.Storage.EfCore.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BlazorMemory.Storage.EfCore.Tests;

/// <summary>
/// Integration-style tests for EfCoreMemoryStore using the EF Core InMemory provider.
/// These tests cover real EF Core operations without needing a live database.
/// </summary>
public class EfCoreMemoryStoreTests : IDisposable
{
    private readonly MemoryDbContext _db;
    private readonly EfCoreMemoryStore<MemoryDbContext> _store;

    public EfCoreMemoryStoreTests()
    {
        var options = new DbContextOptionsBuilder<MemoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // fresh DB per test
            .Options;

        _db = new MemoryDbContext(options);
        _store = new EfCoreMemoryStore<MemoryDbContext>(_db);
    }

    public void Dispose() => _db.Dispose();

    private static MemoryEntry MakeEntry(
        string id = "mem_1",
        string userId = "user_1",
        string content = "User is a software engineer",
        float[]? embedding = null) => new()
    {
        Id        = id,
        UserId    = userId,
        Content   = content,
        Embedding = embedding ?? [1f, 0f, 0f],
        LearnedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task AddAsync_PersistsEntry_ToDatabase()
    {
        var entry = MakeEntry();
        await _store.AddAsync(entry);

        var inDb = await _db.Set<MemoryEntryEntity>().FindAsync(entry.Id);
        inDb.Should().NotBeNull();
        inDb!.Content.Should().Be(entry.Content);
    }

    [Fact]
    public async Task GetAsync_ReturnsEntry_WithCorrectData()
    {
        var entry = MakeEntry();
        await _store.AddAsync(entry);

        var result = await _store.GetAsync(entry.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(entry.Id);
        result.Content.Should().Be(entry.Content);
        result.UserId.Should().Be(entry.UserId);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_ForMissingId()
    {
        var result = await _store.GetAsync("does_not_exist");
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        var entry = MakeEntry();
        await _store.AddAsync(entry);

        await _store.DeleteAsync(entry.Id);

        var result = await _store.GetAsync(entry.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyEntriesForUser()
    {
        await _store.AddAsync(MakeEntry("m1", "user_1", "Fact A"));
        await _store.AddAsync(MakeEntry("m2", "user_1", "Fact B"));
        await _store.AddAsync(MakeEntry("m3", "user_2", "Fact C"));

        var user1 = await _store.ListAsync("user_1");
        var user2 = await _store.ListAsync("user_2");

        user1.Should().HaveCount(2);
        user2.Should().HaveCount(1);
        user1.Should().OnlyContain(m => m.UserId == "user_1");
    }

    [Fact]
    public async Task UpdateAsync_ChangesContentAndEmbedding()
    {
        var entry = MakeEntry();
        await _store.AddAsync(entry);

        var updated = entry with
        {
            Content   = "User is a senior software engineer",
            Embedding = [0.9f, 0.1f, 0f],
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _store.UpdateAsync(updated);

        var result = await _store.GetAsync(entry.Id);
        result!.Content.Should().Be("User is a senior software engineer");
        result.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ClearAsync_RemovesAllEntriesForUser()
    {
        await _store.AddAsync(MakeEntry("m1", "user_1"));
        await _store.AddAsync(MakeEntry("m2", "user_1"));
        await _store.AddAsync(MakeEntry("m3", "user_2"));

        await _store.ClearAsync("user_1");

        var user1 = await _store.ListAsync("user_1");
        var user2 = await _store.ListAsync("user_2");

        user1.Should().BeEmpty();
        user2.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchSimilarAsync_ReturnsSimilarEntries_AboveThreshold()
    {
        // Pointing same direction as query → similarity ≈ 1.0
        await _store.AddAsync(MakeEntry("m1", "user_1", "Relevant", [1f, 0f, 0f]));
        // Perpendicular → similarity = 0
        await _store.AddAsync(MakeEntry("m2", "user_1", "Irrelevant", [0f, 1f, 0f]));

        var results = await _store.SearchSimilarAsync(
            queryEmbedding: [1f, 0f, 0f],
            userId: "user_1",
            limit: 5,
            threshold: 0.9f);

        results.Should().HaveCount(1);
        results[0].Content.Should().Be("Relevant");
        results[0].RelevanceScore.Should().BeApproximately(1.0f, 0.001f);
    }

    [Fact]
    public async Task SearchSimilarAsync_RespectsLimit()
    {
        for (int i = 0; i < 5; i++)
            await _store.AddAsync(MakeEntry($"m{i}", "user_1", $"Fact {i}", [1f, 0f, 0f]));

        var results = await _store.SearchSimilarAsync(
            queryEmbedding: [1f, 0f, 0f],
            userId: "user_1",
            limit: 3,
            threshold: 0f);

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task Embedding_RoundTrips_Correctly()
    {
        var originalEmbedding = new float[] { 0.123f, -0.456f, 0.789f, 1.0f };
        var entry = MakeEntry(embedding: originalEmbedding);
        await _store.AddAsync(entry);

        var result = await _store.GetAsync(entry.Id);

        result!.Embedding.Should().BeEquivalentTo(originalEmbedding,
            options => options.WithStrictOrdering());
    }
}
