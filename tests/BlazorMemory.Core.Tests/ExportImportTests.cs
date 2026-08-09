using System.Text.Json;
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

public class ExportImportTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static float[] FakeEmbedding() => [0.1f, 0.2f, 0.3f];

    private static (MemoryService sut, InMemoryMemoryStore store, IEmbeddingsProvider embeddings) BuildSut()
    {
        var store      = new InMemoryMemoryStore();
        var embeddings = Substitute.For<IEmbeddingsProvider>();
        var extractor  = Substitute.For<IMemoryExtractor>();

        embeddings.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(FakeEmbedding()));

        var engine = new ExtractionEngine(store, embeddings, extractor,
            NullLogger<ExtractionEngine>.Instance);
        var sut = new MemoryService(store, embeddings, engine,
            NullLogger<MemoryService>.Instance);

        return (sut, store, embeddings);
    }

    private static MemoryEntry MakeEntry(string id, string userId, string content, string? ns = null) =>
        new MemoryEntry
        {
            Id        = id,
            UserId    = userId,
            Content   = content,
            Embedding = FakeEmbedding(),
            LearnedAt = DateTimeOffset.UtcNow,
            Namespace = ns,
        };

    private static string BuildImportJson(string userId, params (string id, string content, string? ns)[] entries)
    {
        var export = new MemoryExport
        {
            UserId     = userId,
            ExportedAt = DateTimeOffset.UtcNow,
            Version    = "1.0",
            Memories   = entries.Select(e => new MemoryExportEntry
            {
                Id        = e.id,
                Content   = e.content,
                Namespace = e.ns,
                LearnedAt = DateTimeOffset.UtcNow,
            }).ToList(),
        };
        return JsonSerializer.Serialize(export, JsonOpts);
    }

    // ── ExportAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportAsync_ReturnsValidJson()
    {
        var (sut, store, _) = BuildSut();
        await store.AddAsync(MakeEntry("m1", "user_1", "User likes coffee"));

        var json = await sut.ExportAsync("user_1");

        var act = () => JsonDocument.Parse(json);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ExportAsync_IncludesAllMemories()
    {
        var (sut, store, _) = BuildSut();
        await store.AddAsync(MakeEntry("m1", "user_1", "Fact A"));
        await store.AddAsync(MakeEntry("m2", "user_1", "Fact B"));
        await store.AddAsync(MakeEntry("m3", "user_1", "Fact C"));

        var json   = await sut.ExportAsync("user_1");
        var export = JsonSerializer.Deserialize<MemoryExport>(json, JsonOpts);

        export!.Memories.Should().HaveCount(3);
        export.Memories.Select(m => m.Content).Should().BeEquivalentTo(["Fact A", "Fact B", "Fact C"]);
    }

    [Fact]
    public async Task ExportAsync_ExcludesEmbeddings()
    {
        var (sut, store, _) = BuildSut();
        await store.AddAsync(MakeEntry("m1", "user_1", "Some fact"));

        var json = await sut.ExportAsync("user_1");

        json.ToLowerInvariant().Should().NotContain("embedding");
    }

    [Fact]
    public async Task ExportAsync_ScopedToNamespace()
    {
        var (sut, store, _) = BuildSut();
        await store.AddAsync(MakeEntry("m1", "user_1", "Work fact",     ns: "work"));
        await store.AddAsync(MakeEntry("m2", "user_1", "Personal fact", ns: "personal"));

        var json   = await sut.ExportAsync("user_1", @namespace: "work");
        var export = JsonSerializer.Deserialize<MemoryExport>(json, JsonOpts);

        export!.Memories.Should().HaveCount(1);
        export.Memories[0].Content.Should().Be("Work fact");
    }

    [Fact]
    public async Task ExportAsync_EmptyWhenNoMemories()
    {
        var (sut, _, _) = BuildSut();

        var json   = await sut.ExportAsync("user_1");
        var export = JsonSerializer.Deserialize<MemoryExport>(json, JsonOpts);

        export!.Memories.Should().BeEmpty();
    }

    // ── ImportAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportAsync_StoresNewMemories()
    {
        var (sut, store, _) = BuildSut();
        var json = BuildImportJson("user_1",
            ("e1", "User likes tea", null),
            ("e2", "User is a developer", null));

        await sut.ImportAsync("user_1", json);

        var all = await store.ListAsync("user_1");
        all.Should().HaveCount(2);
        all.Select(m => m.Content).Should().BeEquivalentTo(["User likes tea", "User is a developer"]);
    }

    [Fact]
    public async Task ImportAsync_SkipsDuplicates()
    {
        var (sut, store, _) = BuildSut();
        await store.AddAsync(MakeEntry("m1", "user_1", "User likes tea"));

        var json = BuildImportJson("user_1", ("e1", "User likes tea", null));
        await sut.ImportAsync("user_1", json);

        var all = await store.ListAsync("user_1");
        all.Should().HaveCount(1);
    }

    [Fact]
    public async Task ImportAsync_RegeneratesEmbeddings()
    {
        var (sut, _, embeddings) = BuildSut();
        var json = BuildImportJson("user_1",
            ("e1", "Fact one", null),
            ("e2", "Fact two", null));

        await sut.ImportAsync("user_1", json);

        await embeddings.Received(2)
            .EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportAsync_RespectsNamespace()
    {
        var (sut, store, _) = BuildSut();
        var json = BuildImportJson("user_1", ("e1", "User likes coffee", null));

        await sut.ImportAsync("user_1", json, @namespace: "work");

        var all = await store.ListAsync("user_1", "work");
        all.Should().HaveCount(1);
        all[0].Namespace.Should().Be("work");
    }

    [Fact]
    public async Task ImportAsync_HandlesEmptyJson()
    {
        var (sut, store, _) = BuildSut();
        var json = BuildImportJson("user_1"); // no entries

        var act = async () => await sut.ImportAsync("user_1", json);
        await act.Should().NotThrowAsync();

        var all = await store.ListAsync("user_1");
        all.Should().BeEmpty();
    }
}
