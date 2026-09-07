using BlazorMemory.Core.Abstractions;
using BlazorMemory.Storage.InMemory;

namespace BlazorMemory.Storage.Contract.Tests;

public sealed class InMemoryMemoryStoreContractTests : MemoryStoreContractTests
{
    protected override Task<IMemoryStore> CreateStoreAsync()
        => Task.FromResult<IMemoryStore>(new InMemoryMemoryStore());
}
