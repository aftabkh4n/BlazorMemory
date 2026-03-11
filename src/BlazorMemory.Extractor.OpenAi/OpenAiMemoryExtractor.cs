using System.Text.Json;
using System.Text.Json.Nodes;
using BlazorMemory.Core;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Extensions;
using BlazorMemory.Core.Models;
using BlazorMemory.Extractor.OpenAi.Prompts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace BlazorMemory.Extractor.OpenAi;

public sealed class OpenAiExtractorOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
}

public sealed class OpenAiMemoryExtractor : IMemoryExtractor
{
    private readonly OpenAiExtractorOptions _options;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public OpenAiMemoryExtractor(IOptions<OpenAiExtractorOptions> options)
    {
        _options = options.Value;
    }

    private ChatClient CreateClient()
    {
        var key = ApiKeyStore.Instance.HasKey ? ApiKeyStore.Instance.ApiKey : _options.ApiKey;
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "OpenAI API key is not configured. Enter your key in the app config panel.");
        return new OpenAIClient(key).GetChatClient(_options.Model);
    }

    public async Task<IReadOnlyList<string>> ExtractFactsAsync(
        string conversation, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(ExtractionPrompts.BuildExtractionSystemPrompt()),
            new UserChatMessage(ExtractionPrompts.BuildExtractionUserPrompt(conversation))
        };
        var response = await CreateClient().CompleteChatAsync(messages, cancellationToken: ct);
        var raw = response.Value.Content[0].Text.Trim()
            .Replace("```json", "").Replace("```", "").Trim();
        try { return JsonSerializer.Deserialize<List<string>>(raw, JsonOpts) ?? []; }
        catch { return []; }
    }

    public async Task<ConsolidationDecision> ConsolidateAsync(
        string newFact, IReadOnlyList<MemoryEntry> similarMemories, CancellationToken ct = default)
    {
        if (similarMemories.Count == 0) return ConsolidationDecision.Add();

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(ExtractionPrompts.BuildConsolidationSystemPrompt()),
            new UserChatMessage(ExtractionPrompts.BuildConsolidationUserPrompt(
                newFact, similarMemories.Select(m => (m.Id, m.Content))))
        };
        var response = await CreateClient().CompleteChatAsync(messages, cancellationToken: ct);
        var raw = response.Value.Content[0].Text.Trim()
            .Replace("```json", "").Replace("```", "").Trim();
        return ParseDecision(raw);
    }

    private static ConsolidationDecision ParseDecision(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            var action = node?["action"]?.GetValue<string>()?.ToUpperInvariant();
            return action switch
            {
                "ADD" => ConsolidationDecision.Add(),
                "NONE" => ConsolidationDecision.None(),
                "UPDATE" => ConsolidationDecision.Update(
                                node!["targetId"]!.GetValue<string>(),
                                node!["updatedContent"]!.GetValue<string>()),
                "DELETE" => ConsolidationDecision.Delete(
                                node!["targetId"]!.GetValue<string>()),
                _ => ConsolidationDecision.Add()
            };
        }
        catch { return ConsolidationDecision.Add(); }
    }
}

public static class OpenAiExtractorExtensions
{
    public static BlazorMemoryBuilder UseOpenAiExtractor(
        this BlazorMemoryBuilder builder, string apiKey = "", string model = "gpt-4o-mini")
    {
        builder.Services.Configure<OpenAiExtractorOptions>(o => { o.ApiKey = apiKey; o.Model = model; });
        builder.Services.AddScoped<IMemoryExtractor, OpenAiMemoryExtractor>();
        return builder;
    }
}