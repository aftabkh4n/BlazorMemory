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

// BlazorMemory — API key starts empty, user enters it in the UI
builder.Services
    .AddBlazorMemory()
    .UseIndexedDbStorage()
    .UseOpenAiEmbeddings(string.Empty)   // lazy — safe with empty key at startup
    .UseOpenAiExtractor(string.Empty);   // lazy — safe with empty key at startup

builder.Services.AddScoped<ChatService>();

await builder.Build().RunAsync();