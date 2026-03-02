# BlazorMemory

<p align="center">
  <img src="docs/assets/logo.png" alt="BlazorMemory Logo" width="120" />
</p>

<p align="center">
  <strong>AI memory layer for .NET — runs in the browser, your server, or both.</strong>
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/BlazorMemory"><img src="https://img.shields.io/nuget/v/BlazorMemory.svg" alt="NuGet" /></a>
  <a href="https://www.nuget.org/packages/BlazorMemory"><img src="https://img.shields.io/nuget/dt/BlazorMemory.svg" alt="Downloads" /></a>
  <a href="https://github.com/yourusername/BlazorMemory/actions"><img src="https://img.shields.io/github/actions/workflow/status/yourusername/BlazorMemory/ci.yml" alt="CI" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License" /></a>
</p>

---

Composable building blocks for adding persistent AI memory to .NET applications.

**No backend required for Blazor WASM** — memories live in IndexedDB, embeddings are computed via a pluggable provider, and everything stays in the user's browser. Drop in a server-side adapter when you're ready to scale.

```csharp
// One line setup
var memory = new MemoryBuilder()
    .UseIndexedDbStorage()           // runs in the browser
    .UseOpenAiEmbeddings(apiKey)
    .UseOpenAiExtractor(apiKey)
    .Build();

// Extract memories from a conversation
await memory.ExtractAsync(
    """
    User: I'm a software engineer at Acme Corp working on backend TypeScript.
    Assistant: That's great! Do you have any preferred frameworks?
    User: I love Hono and Drizzle these days.
    """,
    userId: "user_123"
);

// Query relevant memories
var memories = await memory.QueryAsync("What does the user work on?", userId: "user_123");
// => ["User is a backend engineer at Acme Corp", "User prefers Hono and Drizzle"]
```

---

## Why BlazorMemory?

Most AI memory libraries (like Recall, Mem0) target Node.js or Python and require a backend server, a vector database, and infrastructure you have to manage.

**BlazorMemory takes a different approach:**

| Feature | BlazorMemory | Recall / Mem0 |
|---|---|---|
| Runs in the browser | ✅ (Blazor WASM + IndexedDB) | ❌ Node.js only |
| .NET / C# native | ✅ | ❌ |
| No backend required | ✅ (WASM mode) | ❌ |
| Offline capable | ✅ | ❌ |
| Temporal memory (staleness) | ✅ | ❌ |
| Pluggable DI-first design | ✅ | Partial |
| Works server-side too | ✅ (EF Core adapter) | ✅ |

---

## Features

- **LLM-powered extraction** — Automatically extract facts from conversations using OpenAI or Azure OpenAI
- **Intelligent consolidation** — ADD / UPDATE / DELETE / NONE decisions prevent duplicate memories
- **Vector similarity search** — Find relevant memories semantically, not just by keyword
- **Temporal awareness** — Track when facts were learned and surface stale memories automatically
- **Browser-native storage** — IndexedDB adapter for Blazor WASM, no server needed
- **Pluggable architecture** — Swap storage, embeddings, and extractors via standard DI
- **TypeScript-inspired, C#-idiomatic** — Familiar concepts, idiomatic .NET interfaces
- **Microsoft.Extensions.AI compatible** — Works with any `IChatClient` implementation

---

## Quick Start

### Blazor WASM (browser, no backend)

```bash
dotnet add package BlazorMemory
dotnet add package BlazorMemory.Storage.IndexedDb
dotnet add package BlazorMemory.Embeddings.OpenAi
dotnet add package BlazorMemory.Extractor.OpenAi
```

```csharp
// Program.cs
builder.Services.AddBlazorMemory(options =>
{
    options.UseIndexedDbStorage();
    options.UseOpenAiEmbeddings(builder.Configuration["OpenAI:ApiKey"]!);
    options.UseOpenAiExtractor(builder.Configuration["OpenAI:ApiKey"]!);
});
```

```razor
@inject IMemoryService Memory

@code {
    protected override async Task OnInitializedAsync()
    {
        await Memory.ExtractAsync(conversation, userId: "user_123");
        var facts = await Memory.QueryAsync("What does the user like?", userId: "user_123");
    }
}
```

### ASP.NET Core / Blazor Server (with EF Core)

```bash
dotnet add package BlazorMemory
dotnet add package BlazorMemory.Storage.EfCore
dotnet add package BlazorMemory.Embeddings.AzureOpenAi
dotnet add package BlazorMemory.Extractor.Anthropic
```

```csharp
// Program.cs
builder.Services.AddBlazorMemory(options =>
{
    options.UseEfCoreStorage<AppDbContext>();
    options.UseAzureOpenAiEmbeddings(builder.Configuration["AzureOpenAI:Endpoint"]!);
    options.UseAnthropicExtractor(builder.Configuration["Anthropic:ApiKey"]!);
});
```

---

## How Memory Consolidation Works

When you call `ExtractAsync()`, BlazorMemory doesn't blindly insert new facts. It runs a two-step LLM process:

1. **Extract** — LLM identifies discrete facts from the conversation
2. **Consolidate** — For each fact, BlazorMemory:
   - Searches for similar existing memories via vector similarity
   - Asks the LLM: `ADD`, `UPDATE`, `DELETE`, or `NONE`
   - Executes the decision

```
New fact:    "User's name is Jonathan"
Existing:    [{ content: "User's name is John", learnedAt: 2024-01-01 }]

LLM Decision: UPDATE → "User's name is Jonathan (updated from John)"
```

---

## Temporal Memory

Unlike other libraries, BlazorMemory tracks **when** facts were learned and can surface staleness:

```csharp
var memories = await memory.QueryAsync("Where does the user work?", userId: "user_123",
    options: new QueryOptions
    {
        MaxAgeInDays = 90,        // ignore facts older than 90 days
        IncludeStalenessScore = true
    });

foreach (var m in memories)
{
    Console.WriteLine($"{m.Content} (learned {m.LearnedAt:d}, staleness: {m.StalenessScore:P0})");
}
```

---

## API Reference

```csharp
// Core interface
public interface IMemoryService
{
    // Extract and consolidate memories from conversation text
    Task ExtractAsync(string conversation, string userId, CancellationToken ct = default);

    // Find relevant memories using vector similarity
    Task<IReadOnlyList<MemoryEntry>> QueryAsync(string context, string userId,
        QueryOptions? options = null, CancellationToken ct = default);

    // CRUD
    Task<IReadOnlyList<MemoryEntry>> ListAsync(string userId, CancellationToken ct = default);
    Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default);
    Task UpdateAsync(string id, string content, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task ClearAsync(string userId, CancellationToken ct = default);
}
```

---

## Packages

### Core

| Package | Description |
|---|---|
| `BlazorMemory` | Core interfaces, models, consolidation engine |

### Storage Adapters

| Package | Description |
|---|---|
| `BlazorMemory.Storage.IndexedDb` | Browser IndexedDB via JS Interop (WASM) |
| `BlazorMemory.Storage.LocalStorage` | Browser localStorage for lightweight use |
| `BlazorMemory.Storage.EfCore` | EF Core adapter (SQL Server, PostgreSQL, SQLite) |
| `BlazorMemory.Storage.InMemory` | In-memory adapter for testing |

### Embeddings Providers

| Package | Description |
|---|---|
| `BlazorMemory.Embeddings.OpenAi` | OpenAI text-embedding-3 models |
| `BlazorMemory.Embeddings.AzureOpenAi` | Azure OpenAI embeddings |
| `BlazorMemory.Embeddings.Local` | Local ONNX model via ML.NET (offline/private) |

### Extractors

| Package | Description |
|---|---|
| `BlazorMemory.Extractor.OpenAi` | GPT-based fact extraction |
| `BlazorMemory.Extractor.AzureOpenAi` | Azure OpenAI extractor |
| `BlazorMemory.Extractor.Anthropic` | Claude-based fact extraction |

---

## Architecture

```
┌──────────────────────────────────────────────────┐
│              Your Blazor Application             │
└──────────────────────────────────────────────────┘
                         │
                         ▼
┌──────────────────────────────────────────────────┐
│            IMemoryService (BlazorMemory)         │
│   ExtractAsync()  QueryAsync()  CRUD operations  │
└──────────────────────────────────────────────────┘
         │                  │                │
         ▼                  ▼                ▼
┌──────────────┐  ┌──────────────────┐  ┌──────────────┐
│  IExtractor  │  │  IEmbeddings     │  │  IMemoryStore│
│ OpenAI/Azure │  │  OpenAI/ONNX     │  │ IndexedDB/EF │
│ /Anthropic   │  │  /AzureOpenAI    │  │ Core/InMemory│
└──────────────┘  └──────────────────┘  └──────────────┘
```

---

## Roadmap

- [x] Core interfaces and consolidation engine
- [ ] IndexedDB storage adapter
- [ ] OpenAI embeddings and extractor
- [ ] EF Core storage adapter
- [ ] Azure OpenAI support
- [ ] Local ONNX embeddings (offline mode)
- [ ] Anthropic extractor
- [ ] Temporal staleness scoring
- [ ] Sample: Blazor WASM chatbot with memory
- [ ] Sample: ASP.NET Core API with memory
- [ ] NuGet publishing + CI pipeline

---

## Contributing

Contributions are welcome! Please open an issue first to discuss what you'd like to change.

---

## License

MIT — see [LICENSE](LICENSE) for details.
