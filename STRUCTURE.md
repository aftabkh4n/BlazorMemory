# BlazorMemory — Solution Structure

```
BlazorMemory/
│
├── BlazorMemory.sln
│
├── src/
│   │
│   ├── BlazorMemory.Core/                          ← Core package (NuGet: BlazorMemory)
│   │   ├── Abstractions/
│   │   │   └── Interfaces.cs                       ← IMemoryService, IMemoryStore, IEmbeddingsProvider, IMemoryExtractor
│   │   ├── Models/
│   │   │   └── MemoryEntry.cs                      ← MemoryEntry, QueryOptions, ConsolidationDecision
│   │   ├── Services/
│   │   │   └── MemoryService.cs                    ← Default IMemoryService implementation
│   │   ├── Engine/
│   │   │   ├── ExtractionEngine.cs                 ← Orchestrates extract → consolidate flow
│   │   │   ├── ConsolidationEngine.cs              ← Handles ADD/UPDATE/DELETE/NONE logic
│   │   │   └── StalenessCalculator.cs              ← Temporal staleness scoring
│   │   ├── Extensions/
│   │   │   └── ServiceCollectionExtensions.cs      ← AddBlazorMemory() DI registration
│   │   └── BlazorMemory.Core.csproj
│   │
│   ├── BlazorMemory.Storage.IndexedDb/             ← NuGet: BlazorMemory.Storage.IndexedDb
│   │   ├── IndexedDbMemoryStore.cs                 ← IMemoryStore via JS Interop
│   │   ├── Interop/
│   │   │   ├── IndexedDbInterop.cs                 ← C# JS Interop wrapper
│   │   │   └── blazorMemory.js                     ← JS-side IndexedDB operations
│   │   └── BlazorMemory.Storage.IndexedDb.csproj
│   │
│   ├── BlazorMemory.Storage.EfCore/                ← NuGet: BlazorMemory.Storage.EfCore
│   │   ├── EfCoreMemoryStore.cs                    ← IMemoryStore via EF Core
│   │   ├── MemoryDbContext.cs                      ← DbContext with vector column support
│   │   ├── Entities/
│   │   │   └── MemoryEntryEntity.cs
│   │   └── BlazorMemory.Storage.EfCore.csproj
│   │
│   ├── BlazorMemory.Storage.InMemory/              ← NuGet: BlazorMemory.Storage.InMemory (testing)
│   │   └── InMemoryMemoryStore.cs
│   │
│   ├── BlazorMemory.Embeddings.OpenAi/             ← NuGet: BlazorMemory.Embeddings.OpenAi
│   │   ├── OpenAiEmbeddingsProvider.cs
│   │   └── BlazorMemory.Embeddings.OpenAi.csproj
│   │
│   ├── BlazorMemory.Embeddings.AzureOpenAi/        ← NuGet: BlazorMemory.Embeddings.AzureOpenAi
│   │   └── AzureOpenAiEmbeddingsProvider.cs
│   │
│   ├── BlazorMemory.Embeddings.Local/              ← NuGet: BlazorMemory.Embeddings.Local (offline ONNX)
│   │   ├── LocalEmbeddingsProvider.cs              ← Uses Microsoft.ML.OnnxRuntime
│   │   └── BlazorMemory.Embeddings.Local.csproj
│   │
│   ├── BlazorMemory.Extractor.OpenAi/              ← NuGet: BlazorMemory.Extractor.OpenAi
│   │   ├── OpenAiMemoryExtractor.cs
│   │   ├── Prompts/
│   │   │   ├── ExtractionPrompt.cs
│   │   │   └── ConsolidationPrompt.cs
│   │   └── BlazorMemory.Extractor.OpenAi.csproj
│   │
│   ├── BlazorMemory.Extractor.AzureOpenAi/
│   │   └── AzureOpenAiMemoryExtractor.cs
│   │
│   └── BlazorMemory.Extractor.Anthropic/           ← NuGet: BlazorMemory.Extractor.Anthropic
│       └── AnthropicMemoryExtractor.cs
│
├── samples/
│   │
│   ├── ChatApp.BlazorWasm/                         ← PRIMARY DEMO: Chatbot with browser memory
│   │   ├── Components/
│   │   │   ├── Chat.razor                          ← Main chat UI
│   │   │   └── MemoryPanel.razor                   ← Live memory inspection panel
│   │   ├── Program.cs
│   │   └── ChatApp.BlazorWasm.csproj
│   │
│   └── ChatApp.ServerSide/                         ← ASP.NET Core API demo
│       ├── Controllers/
│       │   ├── ChatController.cs
│       │   └── MemoryController.cs
│       ├── Program.cs
│       └── ChatApp.ServerSide.csproj
│
├── tests/
│   ├── BlazorMemory.Core.Tests/
│   │   ├── ConsolidationEngineTests.cs
│   │   ├── StalenessCalculatorTests.cs
│   │   └── MemoryServiceTests.cs
│   └── BlazorMemory.Storage.InMemory.Tests/
│
├── docs/
│   ├── assets/
│   │   └── logo.png
│   ├── quickstart.md
│   ├── core-concepts.md
│   ├── temporal-memory.md
│   └── adapters.md
│
├── .github/
│   └── workflows/
│       ├── ci.yml                                  ← Build + test on every PR
│       └── nuget-publish.yml                       ← Publish to NuGet on tag
│
└── README.md
```
