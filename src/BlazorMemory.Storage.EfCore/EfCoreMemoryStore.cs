using System.Globalization;
using System.Text.Json;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Engine;
using BlazorMemory.Core.Extensions;
using BlazorMemory.Core.Models;
using BlazorMemory.Storage.EfCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlazorMemory.Storage.EfCore;

public sealed class EfCoreMemoryStore<TContext> : IMemoryStore
    where TContext : MemoryDbContext
{
    private readonly TContext _db;
    private readonly ILogger<EfCoreMemoryStore<TContext>> _logger;

    public EfCoreMemoryStore(TContext db, ILogger<EfCoreMemoryStore<TContext>>? logger = null)
    {
        _db     = db;
        _logger = logger ?? NullLogger<EfCoreMemoryStore<TContext>>.Instance;
    }

    public async Task<string> AddAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        _db.Set<MemoryEntryEntity>().Add(ToEntity(entry));
        await _db.SaveChangesAsync(ct);
        return entry.Id;
    }

    public async Task UpdateAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        var entity = await _db.Set<MemoryEntryEntity>().FindAsync([entry.Id], ct);
        if (entity is null) return;

        entity.Content         = entry.Content;
        entity.EmbeddingJson   = SerializeEmbedding(entry.Embedding);
        entity.MetadataJson    = SerializeMetadata(entry.Metadata);
        entity.Namespace       = entry.Namespace;
        entity.UpdatedAt       = entry.UpdatedAt;
        entity.ImportanceScore = entry.ImportanceScore;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var entity = await _db.Set<MemoryEntryEntity>().FindAsync([id], ct);
        if (entity is null) return;
        _db.Set<MemoryEntryEntity>().Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default)
    {
        var entity = await _db.Set<MemoryEntryEntity>().FindAsync([id], ct);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<MemoryEntry>> ListAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default)
    {
        var query = _db.Set<MemoryEntryEntity>().Where(e => e.UserId == userId);
        if (@namespace is not null) query = query.Where(e => e.Namespace == @namespace);
        return (await query.ToListAsync(ct)).Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<MemoryEntry>> SearchSimilarAsync(
        float[] queryEmbedding,
        string userId,
        int limit,
        float threshold,
        string? @namespace = null,
        CancellationToken ct = default)
    {
        var query = _db.Set<MemoryEntryEntity>().Where(e => e.UserId == userId);
        if (@namespace is not null) query = query.Where(e => e.Namespace == @namespace);

        return (await query.ToListAsync(ct))
            .Select(ToDomain)
            .Select(m => m.WithRelevanceScore(VectorMath.CosineSimilarity(queryEmbedding, m.Embedding)))
            .Where(m => m.RelevanceScore >= threshold)
            .OrderByDescending(m => m.RelevanceScore)
            .Take(limit)
            .ToList();
    }

    public async Task ClearAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default)
    {
        var query = _db.Set<MemoryEntryEntity>().Where(e => e.UserId == userId);
        if (@namespace is not null) query = query.Where(e => e.Namespace == @namespace);
        _db.Set<MemoryEntryEntity>().RemoveRange(await query.ToListAsync(ct));
        await _db.SaveChangesAsync(ct);
    }

    // -- Mapping -------------------------------------------------------------

    private static MemoryEntryEntity ToEntity(MemoryEntry m) => new()
    {
        Id              = m.Id,
        UserId          = m.UserId,
        Content         = m.Content,
        EmbeddingJson   = SerializeEmbedding(m.Embedding),
        MetadataJson    = SerializeMetadata(m.Metadata),
        Namespace       = m.Namespace,
        LearnedAt       = m.LearnedAt,
        UpdatedAt       = m.UpdatedAt,
        ImportanceScore = m.ImportanceScore
    };

    private MemoryEntry ToDomain(MemoryEntryEntity e) => new()
    {
        Id              = e.Id,
        UserId          = e.UserId,
        Content         = e.Content,
        Embedding       = DeserializeEmbedding(e.EmbeddingJson, e.Id),
        Metadata        = DeserializeMetadata(e.MetadataJson),
        Namespace       = e.Namespace,
        LearnedAt       = e.LearnedAt,
        UpdatedAt       = e.UpdatedAt,
        ImportanceScore = e.ImportanceScore
    };

    // Semicolon separator avoids ambiguity with decimal-comma locales.
    private static string SerializeEmbedding(float[] e)
        => string.Join(";", e.Select(v => v.ToString(CultureInfo.InvariantCulture)));

    // Supports the new semicolon format and the legacy comma format.
    // Legacy rows written under a decimal-comma culture are unrecoverable; if
    // parsing fails, an empty embedding is returned and the incident is logged.
    private float[] DeserializeEmbedding(string raw, string entryId)
    {
        try
        {
            return raw.Contains(';')
                ? raw.Split(';').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray()
                : raw.Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to deserialize embedding for memory {Id}; returning empty embedding.", entryId);
            return [];
        }
    }

    private static string? SerializeMetadata(Dictionary<string, string>? m)
        => m is null || m.Count == 0 ? null : JsonSerializer.Serialize(m);

    private static Dictionary<string, string> DeserializeMetadata(string? raw)
        => string.IsNullOrEmpty(raw)
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, string>>(raw) ?? [];
}

public static class EfCoreStorageExtensions
{
    public static BlazorMemoryBuilder UseEfCoreStorage<TContext>(
        this BlazorMemoryBuilder builder)
        where TContext : MemoryDbContext
    {
        builder.Services.AddScoped<IMemoryStore, EfCoreMemoryStore<TContext>>();
        return builder;
    }
}
