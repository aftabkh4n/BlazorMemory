using BlazorMemory.Core.Extensions;
using BlazorMemory.Embeddings.OpenAi;
using BlazorMemory.Extractor.OpenAi;
using BlazorMemory.Storage.IndexedDb;
using ChatApp.BlazorWasm;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ── BlazorMemory ──────────────────────────────────────────────────────────────
// The API key is intentionally read from config so users can set it at runtime.
// In production you would proxy this through your own backend.
var openAiKey = builder.Configuration["OpenAI:ApiKey"] ?? string.Empty;

builder.Services
    .AddBlazorMemory()
    .UseIndexedDbStorage()
    .UseOpenAiEmbeddings(openAiKey)
    .UseOpenAiExtractor(openAiKey);

// ── App Services ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<ChatService>();

await builder.Build().RunAsync();
