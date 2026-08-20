# BlazorMemory — Solution Structure

```
BlazorMemory/
│
├── BlazorMemory.sln
│
├── src/
│   │
│   ├── BlazorMemory.Core/                              ← NuGet: BlazorMemory
│   │   ├── Abstractions/
│   │   │   └── Interfaces.cs                           ← IMemoryService, IMemoryStore, IEmbeddingsProvider, IMemoryExtractor, IAgentMemoryServiceFactory
│   │   ├── Models/
│   │   │   └── MemoryEntry.cs                          ← MemoryEntry, QueryOptions, ConsolidationDecision
│   │   ├── Services/
│   │   │   ├── MemoryService.cs                        ← Default IMemoryService implementation
│   │   │   ├── AgentMemoryService.cs                   ← Per-agent scoped memory operations
│   │   │   └── AgentMemoryServiceFactory.cs            ← Creates AgentMemoryService instances
│   │   ├── Engine/
│   │   │   ├── ExtractionEngine.cs                     ← Orchestrates extract and consolidate flow
│   │   │   ├── ConsolidationEngine.cs                  ← ADD/UPDATE/DELETE/NONE logic
│   │   │   └── StalenessCalculator.cs                  ← Temporal staleness scoring
│   │   ├── Extensions/
│   │   │   └── ServiceCollectionExtensions.cs          ← AddBlazorMemory() DI registration
│   │   └── BlazorMemory.Core.csproj
│   │
│   ├── BlazorMemory.Components/                        ← NuGet: BlazorMemory.Components
│   │   ├── MemoryPanel.razor                           ← Memory list with delete, export, import, feedback
│   │   ├── MemoryGraph.razor                           ← Force-directed memory relationship graph
│   │   ├── MemoryModeToggle.razor                      ← Switch between extract and verbatim modes
│   │   └── BlazorMemory.Components.csproj
│   │
│   ├── BlazorMemory.Storage.IndexedDb/                 ← NuGet: BlazorMemory.Storage.IndexedDb
│   │   ├── IndexedDbMemoryStore.cs                     ← IMemoryStore via JS Interop
│   │   ├── Interop/
│   │   │   ├── IndexedDbInterop.cs                     ← C# JS interop wrapper
│   │   │   └── blazorMemory.js                         ← IndexedDB operations in JS
│   │   └── BlazorMemory.Storage.IndexedDb.csproj
│   │
│   ├── BlazorMemory.Storage.EfCore/                    ← NuGet: BlazorMemory.Storage.EfCore
│   │   ├── EfCoreMemoryStore.cs                        ← IMemoryStore via EF Core
│   │   ├── MemoryDbContext.cs
│   │   └── BlazorMemory.Storage.EfCore.csproj
│   │
│   ├── BlazorMemory.Storage.Pgvector/                  ← NuGet: BlazorMemory.Storage.Pgvector
│   │   ├── PgvectorMemoryStore.cs                      ← IMemoryStore with native pgvector similarity
│   │   ├── PgvectorDbContext.cs                        ← Base DbContext with pgvector column
│   │   └── BlazorMemory.Storage.Pgvector.csproj
│   │
│   ├── BlazorMemory.Storage.InMemory/                  ← NuGet: BlazorMemory.Storage.InMemory
│   │   └── InMemoryMemoryStore.cs                      ← In-process store for tests
│   │
│   ├── BlazorMemory.Embeddings.OpenAi/                 ← NuGet: BlazorMemory.Embeddings.OpenAi
│   │   ├── OpenAiEmbeddingsProvider.cs
│   │   └── BlazorMemory.Embeddings.OpenAi.csproj
│   │
│   ├── BlazorMemory.Embeddings.Ollama/                 ← NuGet: BlazorMemory.Embeddings.Ollama
│   │   ├── OllamaEmbeddingsProvider.cs                 ← HTTP client against localhost:11434
│   │   ├── OllamaEmbeddingsOptions.cs
│   │   ├── ServiceCollectionExtensions.cs
│   │   └── BlazorMemory.Embeddings.Ollama.csproj
│   │
│   ├── BlazorMemory.Embeddings.AzureOpenAi/            ← NuGet: BlazorMemory.Embeddings.AzureOpenAi
│   │   ├── AzureOpenAiEmbeddingsProvider.cs            ← HTTP client, api-key header, deployment URL
│   │   ├── AzureOpenAiEmbeddingsOptions.cs
│   │   ├── ServiceCollectionExtensions.cs
│   │   └── BlazorMemory.Embeddings.AzureOpenAi.csproj
│   │
│   ├── BlazorMemory.Extractor.OpenAi/                  ← NuGet: BlazorMemory.Extractor.OpenAi
│   │   ├── OpenAiMemoryExtractor.cs
│   │   ├── Prompts/
│   │   │   └── ExtractionPrompts.cs
│   │   └── BlazorMemory.Extractor.OpenAi.csproj
│   │
│   ├── BlazorMemory.Extractor.Anthropic/               ← NuGet: BlazorMemory.Extractor.Anthropic
│   │   ├── AnthropicMemoryExtractor.cs
│   │   └── BlazorMemory.Extractor.Anthropic.csproj
│   │
│   ├── BlazorMemory.Extractor.Ollama/                  ← NuGet: BlazorMemory.Extractor.Ollama
│   │   ├── OllamaMemoryExtractor.cs                    ← HTTP client, ExtractJson robustness, retry
│   │   ├── OllamaExtractorOptions.cs
│   │   ├── ServiceCollectionExtensions.cs
│   │   └── BlazorMemory.Extractor.Ollama.csproj
│   │
│   ├── BlazorMemory.Extractor.AzureOpenAi/             ← NuGet: BlazorMemory.Extractor.AzureOpenAi
│   │   ├── AzureOpenAiMemoryExtractor.cs               ← HTTP client, api-key header, deployment URL
│   │   ├── AzureOpenAiExtractorOptions.cs
│   │   ├── ServiceCollectionExtensions.cs
│   │   └── BlazorMemory.Extractor.AzureOpenAi.csproj
│   │
│   └── BlazorMemory.SemanticKernel/                    ← NuGet: BlazorMemory.SemanticKernel
│       ├── BlazorMemoryMemoryStore.cs                  ← Implements SK IMemoryStore over IMemoryStore
│       ├── ServiceCollectionExtensions.cs              ← UseSemanticKernelMemoryStore()
│       └── BlazorMemory.SemanticKernel.csproj
│
├── samples/
│   │
│   └── ChatApp.BlazorWasm/                             ← Chatbot demo with browser memory
│       ├── Components/
│       │   ├── Chat.razor
│       │   └── MemoryPanel.razor
│       ├── Program.cs
│       └── ChatApp.BlazorWasm.csproj
│
├── tests/
│   ├── BlazorMemory.Core.Tests/
│   ├── BlazorMemory.Storage.IndexedDb.Tests/
│   ├── BlazorMemory.Storage.EfCore.Tests/
│   ├── BlazorMemory.Embeddings.Ollama.Tests/
│   ├── BlazorMemory.Embeddings.AzureOpenAi.Tests/
│   ├── BlazorMemory.Extractor.Anthropic.Tests/
│   ├── BlazorMemory.Extractor.Ollama.Tests/
│   └── BlazorMemory.Extractor.AzureOpenAi.Tests/
│
├── assets/
│   ├── demo.gif
│   └── blazormemory-icon.png
│
├── .github/
│   └── workflows/
│       ├── ci.yml                                      ← Build and test on every PR
│       └── nuget-publish.yml                           ← Publish to NuGet on tag
│
├── README.md
└── STRUCTURE.md
```
