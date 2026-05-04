using System.Text.Json;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Engine;
using BlazorMemory.Core.Models;
using Microsoft.Extensions.Logging;

namespace BlazorMemory.Core.Services;

public sealed class MemoryService : IMemoryService
{
    private readonly IMemoryStore           _store;
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
        ILogger<MemoryService> logger)
    {
        _store      = store;
        _embeddings = embeddings;
        _engine     = engine;
        _logger     = logger;
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
}