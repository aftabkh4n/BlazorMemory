using BlazorMemory.Core.Models;
using BlazorMemory.Storage.InMemory;
using FluentAssertions;

namespace BlazorMemory.Core.Tests;

public class InMemoryStoreTests
{
    private static MemoryEntry MakeEntry(string userId = "user_1", string content = "Test") => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        UserId = userId,
        Content = content,
        Embedding = [1f, 0f, 0f],
        LearnedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task AddAsync_StoresEntry_RetrievableById()
    {
        var store = new InMemoryMemoryStore();
        var entry = MakeEntry();

        await store.AddAsync(entry);
        var result = await store.GetAsync(entry.Id);

        result.Should().NotBeNull();
        result!.Content.Should().Be(entry.Content);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        var store = new InMemoryMemoryStore();
        var entry = MakeEntry();
        await store.AddAsync(entry);

        await store.DeleteAsync(entry.Id);
        var result = await store.GetAsync(entry.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyUserEntries()
    {
        var store = new InMemoryMemoryStore();
        await store.AddAsync(MakeEntry("user_1", "Memory A"));
        await store.AddAsync(MakeEntry("user_1", "Memory B"));
        await store.AddAsync(MakeEntry("user_2", "Memory C"));

        var user1Memories = await store.ListAsync("user_1");

        user1Memories.Should().HaveCount(2);
        user1Memories.Should().OnlyContain(m => m.UserId == "user_1");
    }

    [Fact]
    public async Task ClearAsync_RemovesAllUserEntries()
    {
        var store = new InMemoryMemoryStore();
        await store.AddAsync(MakeEntry("user_1"));
        await store.AddAsync(MakeEntry("user_1"));
        await store.AddAsync(MakeEntry("user_2"));

        await store.ClearAsync("user_1");

        var user1 = await store.ListAsync("user_1");
        var user2 = await store.ListAsync("user_2");

        user1.Should().BeEmpty();
        user2.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchSimilarAsync_ReturnsSimilarVectors_AboveThreshold()
    {
        var store = new InMemoryMemoryStore();

        // Vector pointing in same direction as query → high similarity
        await store.AddAsync(MakeEntry("user_1", "Relevant") with { Embedding = [1f, 0f, 0f] });

        // Vector perpendicular to query → similarity = 0
        await store.AddAsync(MakeEntry("user_1", "Irrelevant") with { Embedding = [0f, 1f, 0f] });

        var queryEmbedding = new float[] { 1f, 0f, 0f };
        var results = await store.SearchSimilarAsync(queryEmbedding, "user_1", limit: 5, threshold: 0.9f);

        results.Should().HaveCount(1);
        results[0].Content.Should().Be("Relevant");
        results[0].RelevanceScore.Should().BeApproximately(1.0f, 0.001f);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesContent()
    {
        var store = new InMemoryMemoryStore();
        var entry = MakeEntry();
        await store.AddAsync(entry);

        var updated = entry with { Content = "Updated content", UpdatedAt = DateTimeOffset.UtcNow };
        await store.UpdateAsync(updated);

        var result = await store.GetAsync(entry.Id);
        result!.Content.Should().Be("Updated content");
        result.UpdatedAt.Should().NotBeNull();
    }
}
