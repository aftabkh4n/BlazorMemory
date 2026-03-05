using Xunit;
using BlazorMemory.Core.Models;
using BlazorMemory.Storage.IndexedDb;
using FluentAssertions;
using Microsoft.JSInterop;
using NSubstitute;

namespace BlazorMemory.Storage.IndexedDb.Tests;

/// <summary>
/// IndexedDbMemoryStore delegates all operations to IndexedDbInterop which requires
/// a live browser JS runtime. These tests verify the store's non-JS logic and use
/// a helper shim to exercise the delegation paths.
/// 
/// Full integration testing of the JS interop layer is done via Playwright/bUnit
/// browser tests, not unit tests.
/// </summary>
public class IndexedDbMemoryStoreTests
{
    private static MemoryEntry MakeEntry(string id = "mem_1") => new()
    {
        Id = id,
        UserId = "user_1",
        Content = "User likes C#",
        Embedding = [0.1f, 0.2f, 0.3f],
        LearnedAt = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Creates a JSRuntime mock that returns a working module substitute,
    /// working around NSubstitute's generic interface limitations by using
    /// a hand-rolled shim instead.
    /// </summary>
    private static (FakeJSRuntime jsRuntime, FakeJSModule jsModule) CreateFakes()
    {
        var module = new FakeJSModule();
        var jsRuntime = new FakeJSRuntime(module);
        return (jsRuntime, module);
    }

    [Fact]
    public async Task AddAsync_CallsJsAddEntry_AndReturnsId()
    {
        var (js, module) = CreateFakes();
        var store = new IndexedDbMemoryStore(js);
        var entry = MakeEntry();

        module.AddReturnValue = entry.Id;

        var result = await store.AddAsync(entry);

        result.Should().Be(entry.Id);
        module.LastMethodCalled.Should().Be("addEntry");
    }

    [Fact]
    public async Task DeleteAsync_CallsJsDeleteEntry()
    {
        var (js, module) = CreateFakes();
        var store = new IndexedDbMemoryStore(js);

        await store.DeleteAsync("mem_1");

        module.LastMethodCalled.Should().Be("deleteEntry");
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenJsReturnsNull()
    {
        var (js, module) = CreateFakes();
        var store = new IndexedDbMemoryStore(js);

        module.GetReturnDto = null;

        var result = await store.GetAsync("missing_id");

        result.Should().BeNull();
        module.LastMethodCalled.Should().Be("getEntry");
    }

    [Fact]
    public async Task ClearAsync_CallsJsClearEntries()
    {
        var (js, module) = CreateFakes();
        var store = new IndexedDbMemoryStore(js);

        await store.ClearAsync("user_1");

        module.LastMethodCalled.Should().Be("clearEntries");
    }
}

// ── Test doubles ──────────────────────────────────────────────────────────────

/// <summary>Hand-rolled IJSRuntime shim that returns FakeJSModule for any import call.</summary>
public sealed class FakeJSRuntime : IJSRuntime
{
    private readonly FakeJSModule _module;
    public FakeJSRuntime(FakeJSModule module) => _module = module;

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (identifier == "import")
            return new ValueTask<TValue>((TValue)(object)_module);

        throw new InvalidOperationException($"Unexpected IJSRuntime call: {identifier}");
    }
}

/// <summary>Hand-rolled IJSObjectReference shim that records calls and returns configured values.</summary>
public sealed class FakeJSModule : IJSObjectReference
{
    public string? LastMethodCalled { get; private set; }
    public string AddReturnValue { get; set; } = "mem_1";
    public object? GetReturnDto { get; set; }
    public List<object> ListReturnDtos { get; set; } = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        LastMethodCalled = identifier;
        object? result = identifier switch
        {
            "addEntry" => AddReturnValue,
            "getEntry" => GetReturnDto,
            "listEntries" => ListReturnDtos,
            "searchSimilar" => ListReturnDtos,
            _ => default(TValue)
        };
        return new ValueTask<TValue>((TValue)(result ?? default(TValue)!));
    }

    public ValueTask InvokeVoidAsync(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        LastMethodCalled = identifier;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}