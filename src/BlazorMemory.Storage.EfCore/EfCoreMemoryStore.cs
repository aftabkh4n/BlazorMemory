using System.Text.Json;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Engine;
using BlazorMemory.Core.Extensions;
using BlazorMemory.Core.Models;
using BlazorMemory.Storage.EfCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorMemory.Storage.EfCore;

public sealed class EfCoreMemoryStore<TContext> : IMemoryStore
    where TContext : MemoryDbContext
{
    private readonly TContext _db;

    public EfCoreMemoryStore(TContext db) => _db = db;

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

        entity.Content       = entry.Content;
        entity.EmbeddingJson = SerializeEmbedding(entry.Embedding);
        entity.MetadataJson  = SerializeMetadata(entry.Metadata);
        entity.Namespace     = entry.Namespace;
        entity.UpdatedAt     = entry.UpdatedAt;
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
        return (await query.ToListAsync(ct)).Select(e => ToDomain(e)).ToList();
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
            .Select(e => ToDomain(e).WithRelevanceScore(
                VectorMath.CosineSimilarity(queryEmbedding, DeserializeEmbedding(e.EmbeddingJson))))
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

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static MemoryEntryEntity ToEntity(MemoryEntry m) => new()
    {
        Id            = m.Id,
        UserId        = m.UserId,
        Content       = m.Content,
        EmbeddingJson = SerializeEmbedding(m.Embedding),
        MetadataJson  = SerializeMetadata(m.Metadata),
        Namespace     = m.Namespace,
        LearnedAt     = m.LearnedAt,
        UpdatedAt     = m.UpdatedAt
    };

    private static MemoryEntry ToDomain(MemoryEntryEntity e) => new()
    {
        Id        = e.Id,
        UserId    = e.UserId,
        Content   = e.Content,
        Embedding = DeserializeEmbedding(e.EmbeddingJson),
        Metadata  = DeserializeMetadata(e.MetadataJson),
        Namespace = e.Namespace,
        LearnedAt = e.LearnedAt,
        UpdatedAt = e.UpdatedAt
    };

    private static string SerializeEmbedding(float[] e)
        => string.Join(",", e);

    private static float[] DeserializeEmbedding(string raw)
        => raw.Split(',').Select(float.Parse).ToArray();

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