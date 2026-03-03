using System.Text.Json;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Engine;
using BlazorMemory.Core.Extensions;
using BlazorMemory.Core.Models;
using BlazorMemory.Storage.EfCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorMemory.Storage.EfCore;

/// <summary>
/// IMemoryStore implementation backed by Entity Framework Core.
/// Supports SQL Server, PostgreSQL, SQLite, and any other EF Core provider.
///
/// Vector similarity search is performed in-process using cosine similarity
/// (loads all user memories into memory). For large datasets with PostgreSQL,
/// consider adding pgvector support for server-side vector search.
/// </summary>
public sealed class EfCoreMemoryStore<TContext> : IMemoryStore
    where TContext : DbContext
{
    private readonly TContext _db;
    private static readonly JsonSerializerOptions JsonOpts = new();

    public EfCoreMemoryStore(TContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<string> AddAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        var entity = ToEntity(entry);
        _db.Set<MemoryEntryEntity>().Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        var entity = await _db.Set<MemoryEntryEntity>()
            .FindAsync([entry.Id], ct);

        if (entity is null) return;

        entity.Content       = entry.Content;
        entity.EmbeddingJson = SerializeEmbedding(entry.Embedding);
        entity.UpdatedAt     = entry.UpdatedAt ?? DateTimeOffset.UtcNow;
        entity.MetadataJson  = JsonSerializer.Serialize(entry.Metadata, JsonOpts);

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var entity = await _db.Set<MemoryEntryEntity>().FindAsync([id], ct);
        if (entity is null) return;
        _db.Set<MemoryEntryEntity>().Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default)
    {
        var entity = await _db.Set<MemoryEntryEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        return entity is null ? null : ToDomain(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MemoryEntry>> ListAsync(string userId, CancellationToken ct = default)
    {
        var entities = await _db.Set<MemoryEntryEntity>()
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.UpdatedAt ?? e.LearnedAt)
            .ToListAsync(ct);

        return entities.Select(ToDomain).ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Cosine similarity is computed in-process. All memories for the user
    /// are loaded from the database and scored in memory.
    /// For large datasets (10,000+ memories), consider adding a pgvector
    /// index for server-side approximate nearest neighbour search.
    /// </remarks>
    public async Task<IReadOnlyList<MemoryEntry>> SearchSimilarAsync(
        float[] queryEmbedding,
        string userId,
        int limit,
        float threshold,
        CancellationToken ct = default)
    {
        var entities = await _db.Set<MemoryEntryEntity>()
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .ToListAsync(ct);

        return entities
            .Select(e =>
            {
                var embedding = DeserializeEmbedding(e.EmbeddingJson);
                var score = VectorMath.CosineSimilarity(queryEmbedding, embedding);
                return (entity: e, embedding, score);
            })
            .Where(x => x.score >= threshold)
            .OrderByDescending(x => x.score)
            .Take(limit)
            .Select(x => ToDomain(x.entity, x.embedding).WithRelevanceScore(x.score))
            .ToList();
    }

    /// <inheritdoc />
    public async Task ClearAsync(string userId, CancellationToken ct = default)
    {
        var entities = await _db.Set<MemoryEntryEntity>()
            .Where(e => e.UserId == userId)
            .ToListAsync(ct);

        _db.Set<MemoryEntryEntity>().RemoveRange(entities);
        await _db.SaveChangesAsync(ct);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static MemoryEntryEntity ToEntity(MemoryEntry m) => new()
    {
        Id            = m.Id,
        UserId        = m.UserId,
        Content       = m.Content,
        EmbeddingJson = SerializeEmbedding(m.Embedding),
        LearnedAt     = m.LearnedAt,
        UpdatedAt     = m.UpdatedAt,
        MetadataJson  = JsonSerializer.Serialize(m.Metadata, JsonOpts)
    };

    private static MemoryEntry ToDomain(MemoryEntryEntity e, float[]? embedding = null) => new()
    {
        Id        = e.Id,
        UserId    = e.UserId,
        Content   = e.Content,
        Embedding = embedding ?? DeserializeEmbedding(e.EmbeddingJson),
        LearnedAt = e.LearnedAt,
        UpdatedAt = e.UpdatedAt,
        Metadata  = JsonSerializer.Deserialize<Dictionary<string, string>>(e.MetadataJson, JsonOpts) ?? []
    };

    private static string SerializeEmbedding(float[] embedding) =>
        JsonSerializer.Serialize(embedding, JsonOpts);

    private static float[] DeserializeEmbedding(string json) =>
        JsonSerializer.Deserialize<float[]>(json, JsonOpts) ?? [];
}

// ── DI Extensions ─────────────────────────────────────────────────────────────

/// <summary>Options for the EF Core storage adapter.</summary>
public sealed class EfCoreStorageOptions
{
    /// <summary>
    /// If true, BlazorMemory will call Database.EnsureCreatedAsync() on startup
    /// to create the Memories table automatically. Default: true.
    /// Set to false if you manage migrations yourself.
    /// </summary>
    public bool AutoCreateTable { get; set; } = true;
}

public static class EfCoreStorageExtensions
{
    /// <summary>
    /// Registers the EF Core storage adapter using your existing DbContext.
    ///
    /// Your DbContext must have a DbSet&lt;MemoryEntryEntity&gt; configured.
    /// Call modelBuilder.ApplyBlazorMemoryConfiguration() in OnModelCreating().
    ///
    /// <code>
    /// builder.Services
    ///     .AddBlazorMemory()
    ///     .UseEfCoreStorage&lt;AppDbContext&gt;()
    ///     .UseOpenAiEmbeddings(apiKey)
    ///     .UseOpenAiExtractor(apiKey);
    /// </code>
    /// </summary>
    public static BlazorMemoryBuilder UseEfCoreStorage<TContext>(
        this BlazorMemoryBuilder builder,
        Action<EfCoreStorageOptions>? configure = null)
        where TContext : DbContext
    {
        var options = new EfCoreStorageOptions();
        configure?.Invoke(options);

        builder.Services.AddScoped<IMemoryStore, EfCoreMemoryStore<TContext>>();
        return builder;
    }

    /// <summary>
    /// Registers the EF Core storage adapter using the built-in MemoryDbContext.
    /// Use this if you don't have your own DbContext and want BlazorMemory to
    /// manage its own database.
    ///
    /// <code>
    /// builder.Services.AddDbContext&lt;MemoryDbContext&gt;(options =>
    ///     options.UseSqlite("Data Source=memories.db"));
    ///
    /// builder.Services
    ///     .AddBlazorMemory()
    ///     .UseEfCoreStorage()
    ///     .UseOpenAiEmbeddings(apiKey)
    ///     .UseOpenAiExtractor(apiKey);
    /// </code>
    /// </summary>
    public static BlazorMemoryBuilder UseEfCoreStorage(
        this BlazorMemoryBuilder builder,
        Action<EfCoreStorageOptions>? configure = null)
        => builder.UseEfCoreStorage<MemoryDbContext>(configure);
}
