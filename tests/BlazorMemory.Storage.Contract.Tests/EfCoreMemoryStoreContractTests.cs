using BlazorMemory.Core.Abstractions;
using BlazorMemory.Storage.EfCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorMemory.Storage.Contract.Tests;

public sealed class EfCoreMemoryStoreContractTests : MemoryStoreContractTests, IAsyncLifetime
{
    private SqliteConnection? _connection;
    private MemoryDbContext?  _db;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_db is not null) await _db.DisposeAsync();
        _connection?.Dispose();
    }

    protected override async Task<IMemoryStore> CreateStoreAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MemoryDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new MemoryDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        return new EfCoreMemoryStore<MemoryDbContext>(_db);
    }
}
