using System.Text.Json;
using System.Text.Json.Nodes;
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
    public required string ApiKey { get; set; }

    /// <summary>Chat model to use. Defaults to gpt-4o-mini (fast, cheap, great for structured output).</summary>
    public string Model { get; set; } = "gpt-4o-mini";
}

/// <summary>
/// Fact extractor and consolidation engine backed by OpenAI chat models.
/// </summary>
public sealed class OpenAiMemoryExtractor : IMemoryExtractor
{
    private readonly ChatClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public OpenAiMemoryExtractor(IOptions<OpenAiExtractorOptions> options)
    {
        var openAiClient = new OpenAIClient(options.Value.ApiKey);
        _client = openAiClient.GetChatClient(options.Value.Model);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ExtractFactsAsync(
        string conversation,
        CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(ExtractionPrompts.BuildExtractionSystemPrompt()),
            new UserChatMessage(ExtractionPrompts.BuildExtractionUserPrompt(conversation))
        };

        var response = await _client.CompleteChatAsync(messages, cancellationToken: ct);
        var raw = response.Value.Content[0].Text.Trim();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(raw, JsonOpts) ?? [];
        }
        catch
        {
            // If the model returned something malformed, return empty rather than crash.
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<ConsolidationDecision> ConsolidateAsync(
        string newFact,
        IReadOnlyList<MemoryEntry> similarMemories,
        CancellationToken ct = default)
    {
        if (similarMemories.Count == 0)
            return ConsolidationDecision.Add();

        var existing = similarMemories.Select(m => (m.Id, m.Content));

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(ExtractionPrompts.BuildConsolidationSystemPrompt()),
            new UserChatMessage(ExtractionPrompts.BuildConsolidationUserPrompt(newFact, existing))
        };

        var response = await _client.CompleteChatAsync(messages, cancellationToken: ct);
        var raw = response.Value.Content[0].Text.Trim();

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
                "ADD"    => ConsolidationDecision.Add(),
                "NONE"   => ConsolidationDecision.None(),
                "UPDATE" => ConsolidationDecision.Update(
                                node!["targetId"]!.GetValue<string>(),
                                node!["updatedContent"]!.GetValue<string>()),
                "DELETE" => ConsolidationDecision.Delete(
                                node!["targetId"]!.GetValue<string>()),
                _        => ConsolidationDecision.Add()  // safe fallback
            };
        }
        catch
        {
            return ConsolidationDecision.Add();
        }
    }
}

/// <summary>DI registration extensions.</summary>
public static class OpenAiExtractorExtensions
{
    public static BlazorMemoryBuilder UseOpenAiExtractor(
        this BlazorMemoryBuilder builder,
        string apiKey,
        string model = "gpt-4o-mini")
    {
        builder.Services.Configure<OpenAiExtractorOptions>(o =>
        {
            o.ApiKey = apiKey;
            o.Model  = model;
        });
        builder.Services.AddScoped<IMemoryExtractor, OpenAiMemoryExtractor>();
        return builder;
    }

    public static BlazorMemoryBuilder UseOpenAiExtractor(
        this BlazorMemoryBuilder builder,
        Action<OpenAiExtractorOptions> configure)
    {
        builder.Services.Configure(configure);
        builder.Services.AddScoped<IMemoryExtractor, OpenAiMemoryExtractor>();
        return builder;
    }
}
