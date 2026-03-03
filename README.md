# BlazorMemory

<p align="center">
  <strong>Persistent AI memory for .NET — browser-native, server-ready, no infrastructure required.</strong>
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/BlazorMemory"><img src="https://img.shields.io/nuget/v/BlazorMemory.svg?label=nuget&color=004880" alt="NuGet" /></a>
  <a href="https://www.nuget.org/packages/BlazorMemory"><img src="https://img.shields.io/nuget/dt/BlazorMemory.svg?color=004880" alt="Downloads" /></a>
  <a href="https://github.com/yourusername/BlazorMemory/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/yourusername/BlazorMemory/ci.yml?label=ci" alt="CI" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License" /></a>
</p>

---

I built this because I wanted to add persistent memory to a Blazor WASM app and every library I found assumed Node.js or Python. There was nothing for .NET, and nothing that ran in the browser without a backend.

BlazorMemory is a set of NuGet packages that gives your AI app a memory layer. It extracts facts from conversations, deduplicates them intelligently, and finds relevant ones when you need them. In Blazor WASM it runs entirely in the browser using IndexedDB — no server, no database, works offline. On the server side it plugs into EF Core and whatever database you already have.

```csharp
// Program.cs — Blazor WASM
builder.Services
    .AddBlazorMemory()
    .UseIndexedDbStorage()          // stores everything in the browser
    .UseOpenAiEmbeddings(apiKey)
    .UseOpenAiExtractor(apiKey);
```

```razor
@inject IMemoryService Memory

@code {
    async Task OnUserMessage(string message, string reply)
    {
        // pull relevant memories before responding
        var context = await Memory.QueryAsync(message, userId: currentUserId);

        // extract new facts after the exchange
        await Memory.ExtractAsync($"User: {message}\nAssistant: {reply}", userId: currentUserId);
    }
}
```

## How it works

When you call `ExtractAsync()`, two things happen under the hood. First, an LLM reads the conversation and pulls out discrete facts about the user. Then, for each fact, it searches for similar memories you already have and decides what to do — add it as new, update an existing one, delete a contradicted one, or skip it entirely because you already know.

```
New fact:  "User's name is Jonathan"
Existing:  "User's name is John"  (similarity: 0.91)

Decision: UPDATE → "User's name is Jonathan"
```

This means your memory store stays clean over time instead of accumulating duplicates every conversation.

## What makes it different

Most AI memory libraries (Recall, Mem0, etc.) are built for Node.js or Python. They require you to spin up a service, connect a vector database, and manage infrastructure. That's fine if you're already doing that — but a lot of .NET apps aren't.

BlazorMemory is just a library. You add the NuGet packages, register a few services in DI, and that's it. No new processes, no new databases unless you want them.

The browser-native piece is genuinely different. Memories live in IndexedDB, cosine similarity search runs in JavaScript against the user's local data, and nothing leaves the device except the OpenAI API calls. If you swap in a local ONNX embedding model (on the roadmap), you can make the whole thing work offline.

There's also one feature I haven't seen elsewhere: staleness scoring. Every memory records when it was learned. You can query with a max age, or ask for a staleness score alongside results, which is useful when you care about whether information is still current.

```csharp
var memories = await Memory.QueryAsync("Where does the user work?", userId,
    new QueryOptions
    {
        MaxAgeInDays = 90,
        IncludeStalenessScore = true
    });

// memories[0].StalenessScore => 0.0 (fresh) to 1.0 (stale)
```

## Getting started

### Blazor WASM

```bash
dotnet add package BlazorMemory
dotnet add package BlazorMemory.Storage.IndexedDb
dotnet add package BlazorMemory.Embeddings.OpenAi
dotnet add package BlazorMemory.Extractor.OpenAi
```

```csharp
// Program.cs
builder.Services
    .AddBlazorMemory()
    .UseIndexedDbStorage()
    .UseOpenAiEmbeddings(builder.Configuration["OpenAI:ApiKey"]!)
    .UseOpenAiExtractor(builder.Configuration["OpenAI:ApiKey"]!);
```

### ASP.NET Core / Blazor Server

```bash
dotnet add package BlazorMemory
dotnet add package BlazorMemory.Storage.EfCore
dotnet add package BlazorMemory.Embeddings.OpenAi
dotnet add package BlazorMemory.Extractor.Anthropic
```

```csharp
// Program.cs
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services
    .AddBlazorMemory()
    .UseEfCoreStorage<AppDbContext>()
    .UseOpenAiEmbeddings(builder.Configuration["OpenAI:ApiKey"]!)
    .UseAnthropicExtractor(builder.Configuration["Anthropic:ApiKey"]!);
```

Your `AppDbContext` just needs to call `modelBuilder.ApplyBlazorMemoryConfiguration()` in `OnModelCreating` and that's it — BlazorMemory uses your existing database connection.

## The full API

Everything goes through `IMemoryService`:

```csharp
// Extract facts from a conversation turn (runs extract + consolidate)
await memory.ExtractAsync(conversationText, userId);

// Semantic search — returns memories ranked by relevance
var results = await memory.QueryAsync(userMessage, userId, new QueryOptions
{
    Limit = 5,
    Threshold = 0.7f,          // cosine similarity cutoff
    MaxAgeInDays = 90,         // optional age filter
    IncludeStalenessScore = true
});

// Everything else you'd expect
var all  = await memory.ListAsync(userId);
var one  = await memory.GetAsync(id);
         await memory.UpdateAsync(id, newContent);
         await memory.DeleteAsync(id);
         await memory.ClearAsync(userId);
```

## Packages

**Core**

| Package | Description |
|---|---|
| `BlazorMemory` | Interfaces, consolidation engine, `IMemoryService` |
| `BlazorMemory.Storage.InMemory` | In-memory store — good for tests and prototyping |

**Storage**

| Package | Description |
|---|---|
| `BlazorMemory.Storage.IndexedDb` | Browser IndexedDB via JS interop (Blazor WASM) |
| `BlazorMemory.Storage.EfCore` | EF Core — SQL Server, PostgreSQL, SQLite |

**Embeddings**

| Package | Description |
|---|---|
| `BlazorMemory.Embeddings.OpenAi` | OpenAI text-embedding-3-small / large |

**Extractors**

| Package | Description |
|---|---|
| `BlazorMemory.Extractor.OpenAi` | GPT-4o-mini fact extraction |
| `BlazorMemory.Extractor.Anthropic` | Claude Haiku / Sonnet fact extraction |

## Running the demo

There's a Blazor WASM sample app in `/samples/ChatApp.BlazorWasm` that shows the whole thing working end-to-end — a chat interface with a live memory panel on the side.

```bash
cd samples/ChatApp.BlazorWasm
dotnet run
```

Open `https://localhost:5001`, paste your OpenAI key in the config panel, and start chatting. Tell it your name, where you work, what you're building. Watch the memory panel fill up. Refresh the page — the memories are still there, stored in your browser's IndexedDB.

## Architecture

```
Your Blazor App
      │
      ▼
IMemoryService
      │
  ┌───┴──────────────┬───────────────────┐
  ▼                  ▼                   ▼
IMemoryExtractor  IEmbeddingsProvider  IMemoryStore
(OpenAI/Claude)   (OpenAI)             (IndexedDB / EF Core / InMemory)
```

Every component is swappable. Register your own implementation of any interface and BlazorMemory will use it.

## Roadmap

- [x] Core interfaces and consolidation engine
- [x] InMemory storage adapter
- [x] OpenAI embeddings
- [x] OpenAI extractor
- [x] IndexedDB browser storage adapter
- [x] EF Core storage adapter
- [x] Anthropic Claude extractor
- [x] Temporal staleness scoring
- [x] Blazor WASM sample chatbot
- [ ] Azure OpenAI embeddings + extractor
- [ ] Local ONNX embeddings (offline/private mode)
- [ ] ASP.NET Core API sample
- [ ] NuGet publish

## Contributing

Issues and PRs are welcome. If you're adding a new storage adapter or embedding provider, the `IMemoryStore` and `IEmbeddingsProvider` interfaces are the right starting point — look at `BlazorMemory.Storage.InMemory` for the simplest possible reference implementation.

For bugs, please include the adapter and provider you're using, and a minimal repro if you can.

## License

MIT
