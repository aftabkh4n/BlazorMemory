<div align="center">

# BlazorMemory

**Give your .NET AI assistant persistent memory.**

[![NuGet](https://img.shields.io/nuget/v/BlazorMemory?color=5b8af0&label=NuGet)](https://www.nuget.org/packages/BlazorMemory)
[![NuGet Downloads](https://img.shields.io/nuget/dt/BlazorMemory?color=3ecf8e)](https://www.nuget.org/packages/BlazorMemory)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com)

![BlazorMemory Demo](assets/demo.gif)

</div>

---

BlazorMemory sits between your chat logic and your LLM. It extracts facts from conversations, stores them as vector embeddings, and injects relevant context into future prompts. Your assistant remembers the user across sessions.

It works in Blazor WASM with no backend. Memories live in the browser's IndexedDB. It also works server-side with EF Core if you need SQL storage.

## Quickstart

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
    .UseOpenAiEmbeddings(apiKey)
    .UseOpenAiExtractor(apiKey);
```

```csharp
// In your chat service
public class ChatService(IMemoryService memory)
{
    public async Task<string> ChatAsync(string message, string userId)
    {
        // Pull relevant memories
        var memories = await memory.QueryAsync(message, userId,
            new QueryOptions { Limit = 5, Threshold = 0.65f });

        // Build system prompt
        var context = string.Join("\n", memories.Select(m => $"- {m.Content}"));
        var prompt  = $"You are a helpful assistant.\n\nWhat you know:\n{context}";

        // Call your LLM
        var reply = await CallLlmAsync(prompt, message);

        // Extract and store new facts
        await memory.ExtractAsync($"User: {message}\nAssistant: {reply}", userId);

        return reply;
    }
}
```

## Drop-in component

Add the memory panel to your UI with one line:

```bash
dotnet add package BlazorMemory.Components
```

```razor
<MemoryPanel UserId="@userId" IsOpen="true" />
```

The panel shows stored memories, handles delete and clear, and has built-in export and import buttons. No extra wiring.

## Export and import

Users can download their memories as JSON and restore them later:

```csharp
// Export
var json = await memory.ExportAsync(userId);

// Import
await memory.ImportAsync(userId, json);
```

Embeddings are excluded from the export and re-generated on import. This keeps the file small and makes imports work regardless of which embedding provider the target app uses.

## Namespaces

Scope memories by topic:

```csharp
await memory.ExtractAsync(conversation, userId, namespace: "work");

var results = await memory.QueryAsync(query, userId, new QueryOptions
{
    Namespace = "work"
});
```

## Server-side with EF Core

```bash
dotnet add package BlazorMemory.Storage.EfCore
```

```csharp
builder.Services
    .AddBlazorMemory()
    .UseEfCoreStorage<YourDbContext>()
    .UseOpenAiEmbeddings(apiKey)
    .UseOpenAiExtractor(apiKey);
```

## Use Anthropic instead of OpenAI

```bash
dotnet add package BlazorMemory.Extractor.Anthropic
```

```csharp
builder.Services
    .AddBlazorMemory()
    .UseIndexedDbStorage()
    .UseOpenAiEmbeddings(openAiKey)
    .UseAnthropicExtractor(anthropicKey);
```

## Packages

| Package | Description |
|---------|-------------|
| `BlazorMemory` | Core library |
| `BlazorMemory.Components` | Drop-in MemoryPanel component |
| `BlazorMemory.Storage.IndexedDb` | Browser storage, no backend needed |
| `BlazorMemory.Storage.InMemory` | For testing |
| `BlazorMemory.Storage.EfCore` | SQL Server, PostgreSQL, SQLite |
| `BlazorMemory.Embeddings.OpenAi` | OpenAI text-embedding-3-small |
| `BlazorMemory.Extractor.OpenAi` | OpenAI gpt-4o-mini |
| `BlazorMemory.Extractor.Anthropic` | Anthropic Claude |

## Run the sample app

```bash
git clone https://github.com/aftabkh4n/BlazorMemory
cd BlazorMemory/samples/ChatApp.BlazorWasm
dotnet run
```

Open `http://localhost:5000`, enter your OpenAI key, and start chatting.

## Run the tests

```bash
dotnet test
```

38 tests across 4 test projects.

## Roadmap

- [ ] Relevance feedback ("forget this" / "this is important")
- [ ] pgvector support for PostgreSQL
- [ ] More Blazor components

## License

MIT