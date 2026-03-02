using System.Text.Json;
using BlazorMemory.Core.Models;
using BlazorMemory.Storage.IndexedDb;
using FluentAssertions;
using Microsoft.JSInterop;
using NSubstitute;

namespace BlazorMemory.Storage.IndexedDb.Tests;

/// <summary>
/// Unit tests for IndexedDbMemoryStore.
/// We mock IJSRuntime and IJSObjectReference so no real browser is needed.
/// These tests verify that the C# store correctly marshals calls to/from JS.
/// </summary>
public class IndexedDbMemoryStoreTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static MemoryEntry MakeEntry(string id = "mem_1", string userId = "user_1") => new()
    {
        Id        = id,
        UserId    = userId,
        Content   = "User is a software engineer",
        Embedding = [0.1f, 0.2f, 0.3f],
        LearnedAt = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero)
    };

    private (IndexedDbMemoryStore store, IJSObjectReference mockModule) BuildSut()
    {
        // Mock the JS module reference
        var mockModule = Substitute.For<IJSObjectReference>();

        // Mock IJSRuntime to return our mock module when "import" is called
        var mockJs = Substitute.For<IJSRuntime>();
        mockJs
            .InvokeAsync<IJSObjectReference>(
                "import",
                Arg.Any<CancellationToken>(),
                Arg.Any<object[]>())
            .Returns(ValueTask.FromResult(mockModule));

        var store = new IndexedDbMemoryStore(mockJs);
        return (store, mockModule);
    }

    [Fact]
    public async Task AddAsync_CallsJsAddEntry_AndReturnsId()
    {
        var (store, module) = BuildSut();
        var entry = MakeEntry();

        module
            .InvokeAsync<string>("addEntry", Arg.Any<CancellationToken>(), Arg.Any<object[]>())
            .Returns(ValueTask.FromResult(entry.Id));

        var resultId = await store.AddAsync(entry);

        resultId.Should().Be(entry.Id);
        await module.Received(1).InvokeAsync<string>(
            "addEntry", Arg.Any<CancellationToken>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task DeleteAsync_CallsJsDeleteEntry()
    {
        var (store, module) = BuildSut();

        module
            .InvokeVoidAsync("deleteEntry", Arg.Any<CancellationToken>(), Arg.Any<object[]>())
            .Returns(ValueTask.CompletedTask);

        await store.DeleteAsync("mem_1");

        await module.Received(1).InvokeVoidAsync(
            "deleteEntry", Arg.Any<CancellationToken>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenJsReturnsNull()
    {
        var (store, module) = BuildSut();

        module
            .InvokeAsync<object?>("getEntry", Arg.Any<CancellationToken>(), Arg.Any<object[]>())
            .Returns(ValueTask.FromResult<object?>(null));

        // The real interop returns a typed DTO or null — we verify null propagation
        // by calling the JS mock with a null return
        var result = await store.GetAsync("nonexistent");

        // Because JS returned null, the store should return null
        result.Should().BeNull();
    }

    [Fact]
    public async Task ClearAsync_CallsJsClearEntries()
    {
        var (store, module) = BuildSut();

        module
            .InvokeVoidAsync("clearEntries", Arg.Any<CancellationToken>(), Arg.Any<object[]>())
            .Returns(ValueTask.CompletedTask);

        await store.ClearAsync("user_1");

        await module.Received(1).InvokeVoidAsync(
            "clearEntries", Arg.Any<CancellationToken>(), Arg.Any<object[]>());
    }
}
