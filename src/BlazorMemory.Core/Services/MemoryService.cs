using System.Text.Json;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Engine;
using BlazorMemory.Core.Models;
using Microsoft.Extensions.Logging;

namespace BlazorMemory.Core.Services;

public sealed class MemoryService : IMemoryService
{
    private readonly IMemoryStore           _store;
    private readonly IVerbatimStore?        _verbatimStore;
    private readonly IEmbeddingsProvider    _embeddings;
    private readonly ExtractionEngine       _engine;
    private readonly ILogger<MemoryService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        WriteIndented               = true,
        PropertyNameCaseInsensitive = true
    };

    public MemoryService(
        IMemoryStore store,
        IEmbeddingsProvider embeddings,
        ExtractionEngine engine,
        ILogger<MemoryService> logger,
        IVerbatimStore? verbatimStore = null)
    {
        _store         = store;
        _verbatimStore = verbatimStore;
        _embeddings    = embeddings;
        _engine        = engine;
        _logger        = logger;
    }

    public Task ExtractAsync(
        string conversation,
        string userId,
        string? @namespace = null,
        CancellationToken ct = default)
        => _engine.RunAsync(conversation, userId, @namespace, ct);

    public async Task<IReadOnlyList<MemoryEntry>> QueryAsync(
        string context,
        string userId,
        QueryOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new QueryOptions();
        var embedding = await _embeddings.EmbedAsync(context, ct);

        var results = await _store.SearchSimilarAsync(
            embedding, userId,
            options.Limit,
            options.Threshold,
            options.Namespace,
            ct);

        if (options.MaxAgeInDays.HasValue)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-options.MaxAgeInDays.Value);
            results = results.Where(m => m.LearnedAt >= cutoff).ToList();
        }

        if (options.IncludeStalenessScore)
        {
            results = results
                .Select(m => m.WithStalenessScore(
                    StalenessCalculator.Calculate(m, options.StalenessHalfLifeDays)))
                .ToList();
        }

        return results;
    }

    public Task<IReadOnlyList<MemoryEntry>> ListAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default)
        => _store.ListAsync(userId, @namespace, ct);

    public Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default)
        => _store.GetAsync(id, ct);

    public async Task UpdateAsync(string id, string content, CancellationToken ct = default)
    {
        var existing = await _store.GetAsync(id, ct);
        if (existing is null) return;

        var embedding = await _embeddings.EmbedAsync(content, ct);
        await _store.UpdateAsync(existing with
        {
            Content   = content,
            Embedding = embedding,
            UpdatedAt = DateTimeOffset.UtcNow
        }, ct);
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
        => _store.DeleteAsync(id, ct);

    public Task ClearAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default)
        => _store.ClearAsync(userId, @namespace, ct);

    public async Task<string> ExportAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default)
    {
        var memories = await _store.ListAsync(userId, @namespace, ct);

        var export = new MemoryExport
        {
            UserId      = userId,
            Namespace   = @namespace,
            ExportedAt  = DateTimeOffset.UtcNow,
            Version     = "1.0",
            Memories    = memories.Select(m => new MemoryExportEntry
            {
                Id        = m.Id,
                Content   = m.Content,
                Namespace = m.Namespace,
                LearnedAt = m.LearnedAt,
                UpdatedAt = m.UpdatedAt,
                Metadata  = m.Metadata
            }).ToList()
        };

        return JsonSerializer.Serialize(export, JsonOpts);
    }

    public async Task ImportAsync(
        string userId,
        string json,
        string? @namespace = null,
        CancellationToken ct = default)
    {
        var export = JsonSerializer.Deserialize<MemoryExport>(json, JsonOpts);
        if (export?.Memories is null || export.Memories.Count == 0) return;

        // Load existing content to skip duplicates
        var existing = await _store.ListAsync(userId, @namespace, ct);
        var existingContents = existing
            .Select(m => m.Content.Trim().ToLowerInvariant())
            .ToHashSet();

        foreach (var entry in export.Memories)
        {
            if (existingContents.Contains(entry.Content.Trim().ToLowerInvariant()))
                continue;

            // Re-embed the content for the new user context
            var embedding = await _embeddings.EmbedAsync(entry.Content, ct);

            await _store.AddAsync(new MemoryEntry
            {
                Id        = Guid.NewGuid().ToString("N"),
                UserId    = userId,
                Namespace = @namespace ?? entry.Namespace,
                Content   = entry.Content,
                Embedding = embedding,
                LearnedAt = entry.LearnedAt,
                UpdatedAt = entry.UpdatedAt,
                Metadata  = entry.Metadata ?? []
            }, ct);
        }
    }

    public Task StoreVerbatimAsync(
        string userId,
        string content,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return GetVerbatimStore().StoreAsync(new VerbatimMemory
        {
            Id        = Guid.NewGuid().ToString("N"),
            UserId    = userId,
            Content   = content,
            CreatedAt = DateTimeOffset.UtcNow,
            Metadata  = metadata ?? []
        }, ct);
    }

    public Task<IReadOnlyList<VerbatimMemory>> SearchVerbatimAsync(
        string userId,
        string query,
        int limit = 10,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (limit <= 0) return Task.FromResult<IReadOnlyList<VerbatimMemory>>([]);

        var store = GetVerbatimStore();
        return string.IsNullOrWhiteSpace(query)
            ? store.GetRecentAsync(userId, limit, ct)
            : store.SearchAsync(userId, query, limit, ct);
    }

    public Task DeleteVerbatimAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return GetVerbatimStore().DeleteAsync(id, ct);
    }

    public Task ClearVerbatimAsync(string userId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return GetVerbatimStore().ClearAsync(userId, ct);
    }

    public async Task<string> ExportVerbatimAsync(
        string userId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var memories = await GetVerbatimStore().GetRecentAsync(userId, int.MaxValue, ct);
        var export = new VerbatimMemoryExport
        {
            UserId     = userId,
            ExportedAt = DateTimeOffset.UtcNow,
            Version    = "1.0",
            Memories   = memories.ToList()
        };

        return JsonSerializer.Serialize(export, JsonOpts);
    }

    public async Task ImportVerbatimAsync(
        string userId,
        string json,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var memories = DeserializeVerbatimImport(json);
        if (memories.Count == 0) return;

        var store = GetVerbatimStore();
        var existing = await store.GetRecentAsync(userId, int.MaxValue, ct);
        var existingContents = existing
            .Select(m => m.Content.Trim().ToLowerInvariant())
            .ToHashSet();

        foreach (var memory in memories)
        {
            if (string.IsNullOrWhiteSpace(memory.Content))
                continue;

            var contentKey = memory.Content.Trim().ToLowerInvariant();
            if (!existingContents.Add(contentKey))
                continue;

            await store.StoreAsync(new VerbatimMemory
            {
                Id        = Guid.NewGuid().ToString("N"),
                UserId    = userId,
                Content   = memory.Content,
                CreatedAt = memory.CreatedAt == default ? DateTimeOffset.UtcNow : memory.CreatedAt,
                Metadata  = memory.Metadata
            }, ct);
        }
    }

    private IVerbatimStore GetVerbatimStore()
        => _verbatimStore
           ?? throw new InvalidOperationException(
               "Verbatim memory mode requires an IVerbatimStore registration.");

    private static IReadOnlyList<VerbatimMemory> DeserializeVerbatimImport(string json)
    {
        var export = JsonSerializer.Deserialize<VerbatimMemoryExport>(json, JsonOpts);
        if (export?.Memories is not null)
            return export.Memories;

        return JsonSerializer.Deserialize<List<VerbatimMemory>>(json, JsonOpts)
               ?? [];
    }

    private sealed record VerbatimMemoryExport
    {
        public required string UserId { get; init; }
        public required DateTimeOffset ExportedAt { get; init; }
        public required string Version { get; init; }
        public required List<VerbatimMemory> Memories { get; init; }
    }
}
