# BlazorMemory.Storage.IndexedDb

Browser-native IndexedDB storage adapter for [BlazorMemory](../../README.md).

Runs **entirely client-side** in Blazor WebAssembly — no server, no database, no infrastructure.
Memories persist across page refreshes and browser sessions in the user's own browser.

---

## How It Works

```
Your Blazor Component
        │
        ▼
IMemoryService (BlazorMemory.Core)
        │
        ▼
IndexedDbMemoryStore   ← this package
        │   C# ↔ JS Interop
        ▼
blazorMemory.js        ← ES module loaded at runtime
        │
        ▼
Browser IndexedDB      ← persistent storage, private to the user
```

Vector similarity search (cosine similarity) is computed **in JavaScript** against the
user's stored embeddings, so no server round-trips are needed for queries either.

---

## Installation

```bash
dotnet add package BlazorMemory
dotnet add package BlazorMemory.Storage.IndexedDb
dotnet add package BlazorMemory.Embeddings.OpenAi
dotnet add package BlazorMemory.Extractor.OpenAi
```

## Setup

**Program.cs:**
```csharp
builder.Services
    .AddBlazorMemory()
    .UseIndexedDbStorage()
    .UseOpenAiEmbeddings(builder.Configuration["OpenAI:ApiKey"]!)
    .UseOpenAiExtractor(builder.Configuration["OpenAI:ApiKey"]!);
```

**wwwroot/index.html** — no script tag needed. The JS module is loaded lazily on first use
via Blazor's static web assets system (`_content/BlazorMemory.Storage.IndexedDb/js/blazorMemory.js`).

---

## Performance Notes

IndexedDB vector search loads all memories for a user into memory and computes
cosine similarity in JavaScript. This is fast for typical use cases:

| Memories | Approximate query time |
|---|---|
| < 500     | < 5ms  |
| 500–2000  | 5–20ms |
| 2000+     | Consider the EF Core adapter with pgvector |

For applications expecting thousands of memories per user, use
`BlazorMemory.Storage.EfCore` with a server-side vector index instead.

---

## Data Privacy

All memories are stored in the **user's own browser** using IndexedDB.
No data is sent to any server except for the OpenAI API calls for embedding and extraction.
For fully private, offline use — combine with `BlazorMemory.Embeddings.Local`.
