using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Engine;
using BlazorMemory.Core.Models;
using BlazorMemory.Core.Services;
using BlazorMemory.Storage.InMemory;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BlazorMemory.Core.Tests;

public class VectorMathLengthMismatchTests
{
    [Fact]
    public void CosineSimilarity_ThrowsArgumentException_WhenLengthsDiffer()
    {
        var a = new float[] { 1f, 0f, 0f };
        var b = new float[] { 1f, 0f };

        var act = () => VectorMath.CosineSimilarity(a, b);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("3").And.Contain("2");
    }

    [Fact]
    public void CosineSimilarity_ExceptionMessage_NamesALength()
    {
        var a = new float[] { 1f, 0f, 0f };
        var b = new float[] { 1f, 0f };

        var act = () => VectorMath.CosineSimilarity(a, b);

        act.Should().Throw<ArgumentException>().WithMessage("*3*");
    }

    [Fact]
    public void CosineSimilarity_ExceptionMessage_NamesBLength()
    {
        var a = new float[] { 1f, 0f, 0f };
        var b = new float[] { 1f, 0f };

        var act = () => VectorMath.CosineSimilarity(a, b);

        act.Should().Throw<ArgumentException>().WithMessage("*2*");
    }

    [Fact]
    public void CosineSimilarity_DoesNotThrow_WhenLengthsMatch()
    {
        var a = new float[] { 1f, 0f, 0f };
        var b = new float[] { 0f, 1f, 0f };

        var act = () => VectorMath.CosineSimilarity(a, b);

        act.Should().NotThrow();
    }
}

public class MemoryContextBuilderTests
{
    private static MemoryEntry MakeEntry(string id, string content) => new()
    {
        Id        = id,
        UserId    = "user1",
        Content   = content,
        Embedding = [0.1f, 0.2f, 0.3f],
        LearnedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public void Build_ReturnsEmptyString_WhenListIsEmpty()
    {
        var result = MemoryContextBuilder.Build(Array.Empty<MemoryEntry>());

        result.Should().BeEmpty();
    }

    [Fact]
    public void Build_ContainsMemoryContent_WhenListIsNotEmpty()
    {
        var memories = new[] { MakeEntry("1", "User loves pizza") };

        var result = MemoryContextBuilder.Build(memories);

        result.Should().Contain("User loves pizza");
    }

    [Fact]
    public void Build_DelimitsBlock_WithOpenAndCloseMarkers()
    {
        var memories = new[] { MakeEntry("1", "Some fact") };

        var result = MemoryContextBuilder.Build(memories);

        result.Should().Contain("---");
    }

    [Fact]
    public void Build_Labels_AsReferenceDataNotInstructions()
    {
        var memories = new[] { MakeEntry("1", "Some fact") };

        var result = MemoryContextBuilder.Build(memories);

        (result.ToLowerInvariant().Contains("reference") ||
         result.ToLowerInvariant().Contains("not instructions")).Should().BeTrue();
    }

    [Fact]
    public void Build_IncludesAllMemories()
    {
        var memories = new[]
        {
            MakeEntry("1", "Fact one"),
            MakeEntry("2", "Fact two"),
            MakeEntry("3", "Fact three")
        };

        var result = MemoryContextBuilder.Build(memories);

        result.Should().Contain("Fact one").And.Contain("Fact two").And.Contain("Fact three");
    }

    [Fact]
    public void Build_IncludesOptionalHeader_WhenProvided()
    {
        var memories = new[] { MakeEntry("1", "Some fact") };

        var result = MemoryContextBuilder.Build(memories, "What you remember:");

        result.Should().Contain("What you remember:");
    }

    [Fact]
    public void Build_ReturnsEmptyString_WhenListIsEmptyAndHeaderProvided()
    {
        var result = MemoryContextBuilder.Build(Array.Empty<MemoryEntry>(), "Header");

        result.Should().BeEmpty();
    }
}

public class QueryAsyncModelMismatchTests
{
    private static float[] FakeEmbedding(int dim = 3) => Enumerable.Repeat(0.1f, dim).ToArray();

    private static (MemoryService sut, ILogger<MemoryService> logger) BuildSut(
        IMemoryStore store,
        string currentModel = "openai/text-embedding-3-small")
    {
        var embeddings = Substitute.For<IEmbeddingsProvider>();
        var extractor  = Substitute.For<IMemoryExtractor>();
        var logger     = Substitute.For<ILogger<MemoryService>>();

        embeddings.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(FakeEmbedding());
        embeddings.ModelIdentifier.Returns(currentModel);
        embeddings.Dimensions.Returns(3);

        var engine = new ExtractionEngine(store, embeddings, extractor,
            NullLogger<ExtractionEngine>.Instance);
        var sut = new MemoryService(store, embeddings, engine, logger);

        return (sut, logger);
    }

    private static MemoryEntry MakeStoreEntry(string model, float[]? embedding = null) => new()
    {
        Id             = Guid.NewGuid().ToString("N"),
        UserId         = "user1",
        Content        = "some fact",
        Embedding      = embedding ?? FakeEmbedding(),
        LearnedAt      = DateTimeOffset.UtcNow,
        EmbeddingModel = model
    };

    [Fact]
    public async Task QueryAsync_LogsWarning_WhenStoredModelDiffersFromCurrent()
    {
        var store = new InMemoryMemoryStore();
        await store.AddAsync(MakeStoreEntry("ollama/nomic-embed-text"));
        var (sut, logger) = BuildSut(store, "openai/text-embedding-3-small");

        await sut.QueryAsync("test", "user1", new QueryOptions { Threshold = 0f });

        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task QueryAsync_DoesNotThrow_WhenModelMismatches()
    {
        var store = new InMemoryMemoryStore();
        await store.AddAsync(MakeStoreEntry("ollama/nomic-embed-text"));
        var (sut, _) = BuildSut(store, "openai/text-embedding-3-small");

        var act = async () => await sut.QueryAsync("test", "user1", new QueryOptions { Threshold = 0f });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task QueryAsync_DoesNotWarn_WhenEmbeddingModelIsNull()
    {
        var store = new InMemoryMemoryStore();
        var entry = MakeStoreEntry(null!) with { EmbeddingModel = null };
        await store.AddAsync(entry);
        var (sut, logger) = BuildSut(store, "openai/text-embedding-3-small");

        await sut.QueryAsync("test", "user1", new QueryOptions { Threshold = 0f });

        logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task QueryAsync_DoesNotWarn_WhenModelsMatch()
    {
        var store = new InMemoryMemoryStore();
        await store.AddAsync(MakeStoreEntry("openai/text-embedding-3-small"));
        var (sut, logger) = BuildSut(store, "openai/text-embedding-3-small");

        await sut.QueryAsync("test", "user1", new QueryOptions { Threshold = 0f });

        logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}

public class ReindexAsyncTests
{
    private static float[] FakeEmbedding(float seed = 0.1f) => [seed, seed, seed];

    private static (MemoryService sut, InMemoryMemoryStore store) BuildSut(
        string currentModel = "openai/text-embedding-3-small")
    {
        var store      = new InMemoryMemoryStore();
        var embeddings = Substitute.For<IEmbeddingsProvider>();
        var extractor  = Substitute.For<IMemoryExtractor>();

        embeddings.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(FakeEmbedding(0.9f));
        embeddings.ModelIdentifier.Returns(currentModel);
        embeddings.Dimensions.Returns(3);

        var engine = new ExtractionEngine(store, embeddings, extractor,
            NullLogger<ExtractionEngine>.Instance);
        var sut = new MemoryService(store, embeddings, engine,
            NullLogger<MemoryService>.Instance);

        return (sut, store);
    }

    private static async Task SeedAsync(
        InMemoryMemoryStore store,
        string userId,
        string model,
        int count)
    {
        for (int i = 0; i < count; i++)
            await store.AddAsync(new MemoryEntry
            {
                Id             = Guid.NewGuid().ToString("N"),
                UserId         = userId,
                Content        = $"Fact {i}",
                Embedding      = FakeEmbedding(),
                LearnedAt      = DateTimeOffset.UtcNow,
                EmbeddingModel = model
            });
    }

    [Fact]
    public async Task ReindexAsync_ReembedsMismatchedEntries()
    {
        var (sut, store) = BuildSut("openai/text-embedding-3-small");
        await SeedAsync(store, "user1", "ollama/nomic-embed-text", 3);

        var count = await sut.ReindexAsync("user1");

        count.Should().Be(3);
    }

    [Fact]
    public async Task ReindexAsync_SkipsEntries_WhenModelMatches()
    {
        var (sut, store) = BuildSut("openai/text-embedding-3-small");
        await SeedAsync(store, "user1", "openai/text-embedding-3-small", 3);

        var count = await sut.ReindexAsync("user1");

        count.Should().Be(0);
    }

    [Fact]
    public async Task ReindexAsync_UpdatesEmbeddingModel_OnReindexedEntries()
    {
        var (sut, store) = BuildSut("openai/text-embedding-3-small");
        await SeedAsync(store, "user1", "ollama/nomic-embed-text", 2);

        await sut.ReindexAsync("user1");

        var all = await store.ListAsync("user1");
        all.Should().AllSatisfy(m => m.EmbeddingModel.Should().Be("openai/text-embedding-3-small"));
    }

    [Fact]
    public async Task ReindexAsync_ReportsProgressAfterEachEntry()
    {
        var (sut, store) = BuildSut("openai/text-embedding-3-small");
        await SeedAsync(store, "user1", "ollama/nomic-embed-text", 3);
        var reported = new List<int>();

        await sut.ReindexAsync("user1", progress: new Progress<int>(n => reported.Add(n)));

        // Allow event scheduling time for Progress<T> callbacks
        await Task.Delay(50);
        reported.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public async Task ReindexAsync_OnlyProcessesTargetUser()
    {
        var (sut, store) = BuildSut("openai/text-embedding-3-small");
        await SeedAsync(store, "user1", "ollama/nomic-embed-text", 2);
        await SeedAsync(store, "user2", "ollama/nomic-embed-text", 2);

        var count = await sut.ReindexAsync("user1");

        count.Should().Be(2);
        var user2 = await store.ListAsync("user2");
        user2.Should().AllSatisfy(m => m.EmbeddingModel.Should().Be("ollama/nomic-embed-text"));
    }
}

public class MemoryEnabledChatBuilderTests
{
    private static float[] FakeEmbedding() => [0.1f, 0.2f, 0.3f];

    private static MemoryEntry MakeEntry(string id, string content) => new()
    {
        Id        = id,
        UserId    = "user1",
        Content   = content,
        Embedding = FakeEmbedding(),
        LearnedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task ChatAsync_SystemPrompt_UsesDelimitedBlock_NotRawInterpolation()
    {
        var memory = Substitute.For<IMemoryService>();
        memory.QueryAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<QueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new[] { MakeEntry("1", "User loves pizza") });

        var sut = new MemoryEnabledChat(memory, NullLogger<MemoryEnabledChat>.Instance);

        string? capturedSp = null;
        await sut.ChatAsync("hello", "user1", (sp, _) =>
        {
            capturedSp = sp;
            return Task.FromResult("reply");
        });

        // The block must be delimited, not bare interpolation
        capturedSp.Should().Contain("---");
        capturedSp.Should().Contain("User loves pizza");
    }
}
