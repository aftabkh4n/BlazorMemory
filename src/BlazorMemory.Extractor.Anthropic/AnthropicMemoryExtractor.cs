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
    /// <summary>
    /// Anthropic API key. Can be empty at startup if the user enters it via UI.
    /// The client is created lazily on first use.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Claude model to use. Defaults to claude-haiku-4-5 — fast, cheap, great for
    /// structured JSON tasks. Use claude-sonnet-4-6 for higher quality extraction.
    /// </summary>
    public string Model { get; set; } = "claude-haiku-4-5-20251001";

    /// <summary>Max tokens for extraction responses. 1024 is plenty for JSON arrays.</summary>
    public int MaxTokens { get; set; } = 1024;
}

/// <summary>
/// Claude-powered fact extractor and memory consolidator for BlazorMemory.
///
/// Uses Anthropic's Claude models via the official Anthropic.SDK NuGet package.
/// Claude is particularly well-suited for this task due to its strong
/// instruction-following and reliable JSON output capabilities.
///
/// The client is created lazily so the app can start before the API key is set.
/// </summary>
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ExtractFactsAsync(
        string conversation,
        CancellationToken ct = default)
    {
        var client = CreateClient();

        var request = new MessageParameters
        {
            Model    = _options.Model,
            MaxTokens = _options.MaxTokens,
            System   = [new SystemMessage(ExtractionPrompts.BuildExtractionSystemPrompt())],
            Messages =
            [
                new Message
                {
                    Role    = RoleType.User,
                    Content = [new TextContent { Text = ExtractionPrompts.BuildExtractionUserPrompt(conversation) }]
                }
            ]
        };

        var response = await client.Messages.GetClaudeMessageAsync(request, ct);
        var raw = response.Content.OfType<TextBlock>().FirstOrDefault()?.Text?.Trim() ?? "[]";

        try
        {
            // Strip any accidental markdown code fences
            raw = raw.Replace("```json", "").Replace("```", "").Trim();
            return JsonSerializer.Deserialize<List<string>>(raw, JsonOpts) ?? [];
        }
        catch
        {
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

        var client = CreateClient();
        var existing = similarMemories.Select(m => (m.Id, m.Content));

        var request = new MessageParameters
        {
            Model     = _options.Model,
            MaxTokens = _options.MaxTokens,
            System    = [new SystemMessage(ExtractionPrompts.BuildConsolidationSystemPrompt())],
            Messages  =
            [
                new Message
                {
                    Role    = RoleType.User,
                    Content = [new TextContent { Text = ExtractionPrompts.BuildConsolidationUserPrompt(newFact, existing) }]
                }
            ]
        };

        var response = await client.Messages.GetClaudeMessageAsync(request, ct);
        var raw = response.Content.OfType<TextBlock>().FirstOrDefault()?.Text?.Trim() ?? "{}";

        return ParseDecision(raw.Replace("```json", "").Replace("```", "").Trim());
    }

    // ── Parsing ───────────────────────────────────────────────────────────────

    private static ConsolidationDecision ParseDecision(string json)
    {
        try
        {
            var node   = JsonNode.Parse(json);
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
                _        => ConsolidationDecision.Add()
            };
        }
        catch
        {
            return ConsolidationDecision.Add();
        }
    }
}

// ── DI Extensions ─────────────────────────────────────────────────────────────

public static class AnthropicExtractorExtensions
{
    /// <summary>
    /// Registers the Anthropic Claude extractor for BlazorMemory.
    ///
    /// <code>
    /// builder.Services
    ///     .AddBlazorMemory()
    ///     .UseIndexedDbStorage()
    ///     .UseOpenAiEmbeddings(openAiKey)
    ///     .UseAnthropicExtractor(anthropicKey);
    /// </code>
    /// </summary>
    public static BlazorMemoryBuilder UseAnthropicExtractor(
        this BlazorMemoryBuilder builder,
        string apiKey,
        string model = "claude-haiku-4-5-20251001")
    {
        builder.Services.Configure<AnthropicExtractorOptions>(o =>
        {
            o.ApiKey = apiKey;
            o.Model  = model;
        });
        builder.Services.AddScoped<IMemoryExtractor, AnthropicMemoryExtractor>();
        return builder;
    }

    /// <summary>Configure via options pattern.</summary>
    public static BlazorMemoryBuilder UseAnthropicExtractor(
        this BlazorMemoryBuilder builder,
        Action<AnthropicExtractorOptions> configure)
    {
        builder.Services.Configure(configure);
        builder.Services.AddScoped<IMemoryExtractor, AnthropicMemoryExtractor>();
        return builder;
    }
}
