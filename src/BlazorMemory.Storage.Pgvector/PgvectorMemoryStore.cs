using System.Text.Json;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Extensions;
using BlazorMemory.Core.Models;
using BlazorMemory.Storage.Pgvector.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace BlazorMemory.Storage.Pgvector;

public sealed class PgvectorMemoryStore<TContext> : IMemoryStore
    where TContext : PgvectorMemoryDbContext
{
    private readonly TContext _db;

    public PgvectorMemoryStore(TContext db) => _db = db;

    public async Task<string> AddAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        _db.Set<PgvectorMemoryEntry>().Add(ToEntity(entry));
        await _db.SaveChangesAsync(ct);
        return entry.Id;
    }

    public async Task UpdateAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        var entity = await _db.Set<PgvectorMemoryEntry>().FindAsync([entry.Id], ct);
        if (entity is null) return;

        entity.Content         = entry.Content;
        entity.Embedding       = new Vector(entry.Embedding);
        entity.MetadataJson    = SerializeMetadata(entry.Metadata);
        entity.Namespace       = entry.Namespace;
        entity.UpdatedAt       = entry.UpdatedAt;
        entity.ImportanceScore = entry.ImportanceScore;

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var entity = await _db.Set<PgvectorMemoryEntry>().FindAsync([id], ct);
        if (entity is null) return;
        _db.Set<PgvectorMemoryEntry>().Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default)
    {
        var entity = await _db.Set<PgvectorMemoryEntry>().FindAsync([id], ct);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<MemoryEntry>> ListAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default)
    {
        var query = _db.Set<PgvectorMemoryEntry>().Where(e => e.UserId == userId);
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
        var queryVector = new Vector(queryEmbedding);

        var query = _db.Set<PgvectorMemoryEntry>()
            .Where(e => e.UserId == userId)
            .Where(e => 1 - e.Embedding.CosineDistance(queryVector) >= threshold);

        if (@namespace is not null)
            query = query.Where(e => e.Namespace == @namespace);

        var results = await query
            .OrderBy(e => e.Embedding.CosineDistance(queryVector))
            .Take(limit)
            .ToListAsync(ct);

        return results
            .Select(e =>
            {
                var similarity = 1 - e.Embedding.CosineDistance(queryVector);
                return ToDomain(e).WithRelevanceScore((float)similarity);
            })
            .ToList();
    }

    public async Task ClearAsync(
        string userId,
        string? @namespace = null,
        CancellationToken ct = default)
    {
        var query = _db.Set<PgvectorMemoryEntry>().Where(e => e.UserId == userId);
        if (@namespace is not null) query = query.Where(e => e.Namespace == @namespace);
        _db.Set<PgvectorMemoryEntry>().RemoveRange(await query.ToListAsync(ct));
        await _db.SaveChangesAsync(ct);
    }

    private static PgvectorMemoryEntry ToEntity(MemoryEntry m) => new()
    {
        Id             = m.Id,
        UserId         = m.UserId,
        Content        = m.Content,
        Embedding      = new Vector(m.Embedding),
        MetadataJson   = SerializeMetadata(m.Metadata),
        Namespace      = m.Namespace,
        LearnedAt      = m.LearnedAt,
        UpdatedAt      = m.UpdatedAt,
        ImportanceScore = m.ImportanceScore
    };

    private static MemoryEntry ToDomain(PgvectorMemoryEntry e) => new()
    {
        Id             = e.Id,
        UserId         = e.UserId,
        Content        = e.Content,
        Embedding      = e.Embedding.ToArray(),
        Metadata       = DeserializeMetadata(e.MetadataJson),
        Namespace      = e.Namespace,
        LearnedAt      = e.LearnedAt,
        UpdatedAt      = e.UpdatedAt,
        ImportanceScore = e.ImportanceScore
    };

    private static string? SerializeMetadata(Dictionary<string, string>? m)
        => m is null || m.Count == 0 ? null : JsonSerializer.Serialize(m);

    private static Dictionary<string, string> DeserializeMetadata(string? raw)
        => string.IsNullOrEmpty(raw)
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, string>>(raw) ?? [];
}

public static class PgvectorStorageExtensions
{
    public static BlazorMemoryBuilder UsePgvectorStorage<TContext>(
        this BlazorMemoryBuilder builder)
        where TContext : PgvectorMemoryDbContext
    {
        builder.Services.AddScoped<IMemoryStore, PgvectorMemoryStore<TContext>>();
        return builder;
    }
}