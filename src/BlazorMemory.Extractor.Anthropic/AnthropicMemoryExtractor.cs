using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Extensions;
using BlazorMemory.Core.Models;
using BlazorMemory.Extractor.Anthropic.Prompts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorMemory.Extractor.Anthropic;

public sealed class AnthropicExtractorOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-haiku-4-5-20251001";
    public int MaxTokens { get; set; } = 1024;
}

public sealed class AnthropicMemoryExtractor : IMemoryExtractor
{
    private readonly AnthropicExtractorOptions _options;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AnthropicMemoryExtractor(IOptions<AnthropicExtractorOptions> options)
    {
        _options = options.Value;
    }

    private AnthropicClient CreateClient()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException(
                "Anthropic API key is not configured. Set AnthropicExtractorOptions.ApiKey.");
        return new AnthropicClient(_options.ApiKey);
    }

    public async Task<IReadOnlyList<string>> ExtractFactsAsync(
        string conversation,
        CancellationToken ct = default)
    {
        var client = CreateClient();

        var request = new MessageParameters
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            System = new List<SystemMessage>
            {
                new SystemMessage(ExtractionPrompts.BuildExtractionSystemPrompt())
            },
            Messages = new List<Message>
            {
                new Message(RoleType.User,
                    ExtractionPrompts.BuildExtractionUserPrompt(conversation))
            }
        };

        var response = await client.Messages.GetClaudeMessageAsync(request, ct);
        var raw = response.Message.ToString().Trim()
            .Replace("```json", "").Replace("```", "").Trim();

        try { return JsonSerializer.Deserialize<List<string>>(raw, JsonOpts) ?? []; }
        catch { return []; }
    }

    public async Task<ConsolidationDecision> ConsolidateAsync(
        string newFact,
        IReadOnlyList<MemoryEntry> similarMemories,
        CancellationToken ct = default)
    {
        if (similarMemories.Count == 0)
            return ConsolidationDecision.Add();

        var client = CreateClient();
        var existing = similarMemories.Select(m => (m.Id, m.Content));

        var request = new MessageParameters
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            System = new List<SystemMessage>
            {
                new SystemMessage(ExtractionPrompts.BuildConsolidationSystemPrompt())
            },
            Messages = new List<Message>
            {
                new Message(RoleType.User,
                    ExtractionPrompts.BuildConsolidationUserPrompt(newFact, existing))
            }
        };

        var response = await client.Messages.GetClaudeMessageAsync(request, ct);
        var raw = response.Message.ToString().Trim()
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

public static class AnthropicExtractorExtensions
{
    public static BlazorMemoryBuilder UseAnthropicExtractor(
        this BlazorMemoryBuilder builder,
        string apiKey,
        string model = "claude-haiku-4-5-20251001")
    {
        builder.Services.Configure<AnthropicExtractorOptions>(o =>
        {
            o.ApiKey = apiKey;
            o.Model = model;
        });
        builder.Services.AddScoped<IMemoryExtractor, AnthropicMemoryExtractor>();
        return builder;
    }

    public static BlazorMemoryBuilder UseAnthropicExtractor(
        this BlazorMemoryBuilder builder,
        Action<AnthropicExtractorOptions> configure)
    {
        builder.Services.Configure(configure);
        builder.Services.AddScoped<IMemoryExtractor, AnthropicMemoryExtractor>();
        return builder;
    }
}