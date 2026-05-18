using Xunit;
using BlazorMemory.Core.Models;
using BlazorMemory.Storage.InMemory;
using FluentAssertions;

namespace BlazorMemory.Core.Tests;

public class InMemoryVerbatimStoreTests
{
    private static VerbatimMemory Make(string userId, string content, DateTimeOffset? createdAt = null) =>
        new()
        {
            Id        = Guid.NewGuid().ToString("N"),
            UserId    = userId,
            Content   = content,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };

    [Fact]
    public async Task StoreAsync_PersistsMemory()
    {
        var store  = new InMemoryVerbatimStore();
        var memory = Make("u1", "hello world");

        await store.StoreAsync(memory);

        var results = await store.GetRecentAsync("u1");
        results.Should().ContainSingle(m => m.Id == memory.Id);
    }

    [Fact]
    public async Task StoreAsync_Upserts_WhenSameId()
    {
        var store = new InMemoryVerbatimStore();
        var id    = Guid.NewGuid().ToString("N");

        await store.StoreAsync(new VerbatimMemory { Id = id, UserId = "u1", Content = "original", CreatedAt = DateTimeOffset.UtcNow });
        await store.StoreAsync(new VerbatimMemory { Id = id, UserId = "u1", Content = "updated",  CreatedAt = DateTimeOffset.UtcNow });

        var results = await store.GetRecentAsync("u1");
        results.Should().ContainSingle();
        results[0].Content.Should().Be("updated");
    }

    [Fact]
    public async Task SearchAsync_FindsBySubstring_CaseInsensitive()
    {
        var store = new InMemoryVerbatimStore();
        await store.StoreAsync(Make("u1", "The Quick Brown Fox"));
        await store.StoreAsync(Make("u1", "completely unrelated"));

        var results = await store.SearchAsync("u1", "quick brown");

        results.Should().ContainSingle(m => m.Content == "The Quick Brown Fox");
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmpty_WhenNoMatch()
    {
        var store = new InMemoryVerbatimStore();
        await store.StoreAsync(Make("u1", "hello world"));

        var results = await store.SearchAsync("u1", "xyz_no_match");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmpty_ForUnknownUser()
    {
        var store = new InMemoryVerbatimStore();

        var results = await store.SearchAsync("ghost", "anything");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_RespectsLimit()
    {
        var store = new InMemoryVerbatimStore();
        for (var i = 0; i < 5; i++)
            await store.StoreAsync(Make("u1", $"fact {i}"));

        var results = await store.SearchAsync("u1", "fact", limit: 2);

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAsync_DoesNotReturnOtherUsersData()
    {
        var store = new InMemoryVerbatimStore();
        await store.StoreAsync(Make("u1", "secret for u1"));
        await store.StoreAsync(Make("u2", "secret for u2"));

        var results = await store.SearchAsync("u1", "secret");

        results.Should().ContainSingle(m => m.UserId == "u1");
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsMostRecentFirst()
    {
        var store = new InMemoryVerbatimStore();
        var now   = DateTimeOffset.UtcNow;
        await store.StoreAsync(Make("u1", "older",  now.AddHours(-2)));
        await store.StoreAsync(Make("u1", "newest", now));
        await store.StoreAsync(Make("u1", "middle", now.AddHours(-1)));

        var results = await store.GetRecentAsync("u1");

        results[0].Content.Should().Be("newest");
        results[1].Content.Should().Be("middle");
        results[2].Content.Should().Be("older");
    }

    [Fact]
    public async Task GetRecentAsync_RespectsLimit()
    {
        var store = new InMemoryVerbatimStore();
        for (var i = 0; i < 10; i++)
            await store.StoreAsync(Make("u1", $"fact {i}"));

        var results = await store.GetRecentAsync("u1", limit: 3);

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsEmpty_ForUnknownUser()
    {
        var store = new InMemoryVerbatimStore();

        var results = await store.GetRecentAsync("nobody");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_RemovesById()
    {
        var store  = new InMemoryVerbatimStore();
        var memory = Make("u1", "to delete");
        await store.StoreAsync(memory);

        await store.DeleteAsync(memory.Id);

        var results = await store.GetRecentAsync("u1");
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_DoesNotAffectOtherEntries()
    {
        var store   = new InMemoryVerbatimStore();
        var target  = Make("u1", "remove me");
        var keeper  = Make("u1", "keep me");
        await store.StoreAsync(target);
        await store.StoreAsync(keeper);

        await store.DeleteAsync(target.Id);

        var results = await store.GetRecentAsync("u1");
        results.Should().ContainSingle(m => m.Id == keeper.Id);
    }

    [Fact]
    public async Task DeleteAsync_DoesNotAffectOtherUsers()
    {
        var store = new InMemoryVerbatimStore();
        var m1    = Make("u1", "user1 memory");
        var m2    = Make("u2", "user2 memory");
        await store.StoreAsync(m1);
        await store.StoreAsync(m2);

        await store.DeleteAsync(m1.Id);

        var u2Results = await store.GetRecentAsync("u2");
        u2Results.Should().ContainSingle(m => m.Id == m2.Id);
    }

    [Fact]
    public async Task ClearAsync_RemovesAllForUser()
    {
        var store = new InMemoryVerbatimStore();
        await store.StoreAsync(Make("u1", "fact 1"));
        await store.StoreAsync(Make("u1", "fact 2"));

        await store.ClearAsync("u1");

        var results = await store.GetRecentAsync("u1");
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearAsync_DoesNotAffectOtherUser()
    {
        var store = new InMemoryVerbatimStore();
        await store.StoreAsync(Make("u1", "u1 fact"));
        await store.StoreAsync(Make("u2", "u2 fact"));

        await store.ClearAsync("u1");

        var u2Results = await store.GetRecentAsync("u2");
        u2Results.Should().ContainSingle();
    }
}
