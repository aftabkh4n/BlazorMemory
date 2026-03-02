# ChatApp.BlazorWasm

Demo chatbot showcasing BlazorMemory running entirely in the browser.

## What it demonstrates

- AI assistant that remembers facts about you across the whole conversation
- Memories stored in **browser IndexedDB** — no server, no database
- Live memory panel showing what the AI has learned about you
- Memory extraction happens in the background after each message
- Delete individual memories or clear all

## Running locally

```bash
cd samples/ChatApp.BlazorWasm
dotnet run
```

Open https://localhost:5001 — enter your OpenAI API key in the config panel and start chatting.

## How to get an OpenAI API key

1. Go to https://platform.openai.com
2. Sign in → API Keys → Create new secret key
3. Paste it into the config panel in the app

Your key stays in browser memory only — it is never stored or sent anywhere except directly to OpenAI.

## Architecture

```
Chat.razor
    │
    ▼
ChatService.cs
    ├── IMemoryService.QueryAsync()    ← inject memories into system prompt
    ├── OpenAI GPT-4o-mini             ← generate reply
    └── IMemoryService.ExtractAsync()  ← extract new memories (background)
            │
            ▼
    IndexedDbMemoryStore               ← browser IndexedDB
    OpenAiEmbeddingsProvider           ← text-embedding-3-small
    OpenAiMemoryExtractor              ← gpt-4o-mini
```
