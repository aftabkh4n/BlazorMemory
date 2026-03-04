using Xunit;
using BlazorMemory.Core.Models;
using BlazorMemory.Storage.IndexedDb;
using BlazorMemory.Storage.IndexedDb.Interop;
using FluentAssertions;
using Microsoft.JSInterop;
using NSubstitute;

namespace BlazorMemory.Storage.IndexedDb.Tests;

/// <summary>
/// Tests IndexedDbMemoryStore by injecting a mock IJSRuntime and verifying
/// that the correct JS interop calls are made.
/// Requires [InternalsVisibleTo("BlazorMemory.Storage.IndexedDb.Tests")] in the source project.
/// </summary>
public class IndexedDbMemoryStoreTests
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IJSObjectReference _jsModule;
    private readonly IndexedDbInterop _interop;
    private readonly IndexedDbMemoryStore _store;

    public IndexedDbMemoryStoreTests()
    {
        _jsRuntime = Substitute.For<IJSRuntime>();
        _jsModule = Substitute.For<IJSObjectReference>();

        // Intercept the ES module import that IndexedDbInterop performs lazily
        ((IJSRuntime)_jsRuntime)
            .InvokeAsync<IJSObjectReference>(
                "import",
                Arg.Any<object[]>())
            .Returns(new ValueTask<IJSObjectReference>(_jsModule));

        _interop = new IndexedDbInterop(_jsRuntime);
        _store = new IndexedDbMemoryStore(_interop);
    }

    private static MemoryEntry MakeEntry(string id = "mem_1") => new()
    {
        Id = id,
        UserId = "user_1",
        Content = "User likes C#",
        Embedding = [0.1f, 0.2f, 0.3f],
        LearnedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task AddAsync_CallsJsAddEntry_AndReturnsId()
    {
        var entry = MakeEntry();

        _jsModule
            .InvokeAsync<string>("addEntry", Arg.Any<object[]>())
            .Returns(new ValueTask<string>(entry.Id));

        var result = await _store.AddAsync(entry);

        result.Should().Be(entry.Id);
        await _jsModule.Received(1).InvokeAsync<string>("addEntry", Arg.Any<object[]>());
    }

    [Fact]
    public async Task DeleteAsync_CallsJsDeleteEntry()
    {
        _jsModule
            .InvokeVoidAsync("deleteEntry", Arg.Any<object[]>())
            .Returns(ValueTask.CompletedTask);

        await _store.DeleteAsync("mem_1");

        await _jsModule.Received(1).InvokeVoidAsync("deleteEntry", Arg.Any<object[]>());
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenJsReturnsNull()
    {
        _jsModule
            .InvokeAsync<MemoryEntryDto?>("getEntry", Arg.Any<object[]>())
            .Returns(new ValueTask<MemoryEntryDto?>(default(MemoryEntryDto)));

        var result = await _store.GetAsync("missing_id");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ClearAsync_CallsJsClearEntries()
    {
        _jsModule
            .InvokeVoidAsync("clearEntries", Arg.Any<object[]>())
            .Returns(ValueTask.CompletedTask);

        await _store.ClearAsync("user_1");

        await _jsModule.Received(1).InvokeVoidAsync("clearEntries", Arg.Any<object[]>());
    }
}