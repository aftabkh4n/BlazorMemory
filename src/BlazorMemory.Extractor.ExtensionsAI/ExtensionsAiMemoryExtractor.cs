using System.Text.Json;
using System.Text.Json.Nodes;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Extensions;
using BlazorMemory.Core.Models;
using BlazorMemory.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlazorMemory.Extractor.ExtensionsAI;

/// <summary>
/// Adapts any Microsoft.Extensions.AI <see cref="IChatClient"/> to BlazorMemory's
/// <see cref="IMemoryExtractor"/>. Uses the same prompts as the OpenAI extractor
/// and the same JSON-robustness (fence stripping, one retry with a stricter
/// prompt) as the Ollama extractor.
/// </summary>
public sealed class ExtensionsAiMemoryExtractor : IMemoryExtractor
{
    private readonly IChatClient                              _client;
    private readonly ILogger<ExtensionsAiMemoryExtractor>     _logger;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private const string StrictJsonSuffix =
        "\n\nIMPORTANT: Reply with ONLY the JSON. No markdown, no code fences, no explanation.";

    public ExtensionsAiMemoryExtractor(
        IChatClient client,
        ILogger<ExtensionsAiMemoryExtractor>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? NullLogger<ExtensionsAiMemoryExtractor>.Instance;
    }

    public async Task<IReadOnlyList<string>> ExtractFactsAsync(
        string conversation, CancellationToken ct = default)
    {
        var system = ExtractionPrompts.ExtractionSystem;
        var user   = $"Extract facts about the user from this conversation:\n\n{conversation}";

        var raw = await ChatAsync(system, user, ct).ConfigureAwait(false);
        var result = TryDeserializeList(JsonExtractor.ExtractJson(raw));
        if (result is not null) return result;

        _logger.LogWarning(
            "ExtensionsAiMemoryExtractor: failed to parse ExtractFacts response; retrying with strict prompt.");
        var raw2 = await ChatAsync(system, user + StrictJsonSuffix, ct).ConfigureAwait(false);
        var result2 = TryDeserializeList(JsonExtractor.ExtractJson(raw2));
        if (result2 is not null) return result2;

        _logger.LogError(
            "ExtensionsAiMemoryExtractor: retry also failed to parse ExtractFacts response; returning empty list.");
        return [];
    }

    public async Task<ConsolidationDecision> ConsolidateAsync(
        string newFact, IReadOnlyList<MemoryEntry> similarMemories, CancellationToken ct = default)
    {
        if (similarMemories.Count == 0) return ConsolidationDecision.Add();

        var system   = ExtractionPrompts.ConsolidationSystem;
        var existing = string.Join("\n", similarMemories.Select(m => $"- id:{m.Id} | {m.Content}"));
        var user     = $"New fact: {newFact}\n\nExisting memories:\n{existing}";

        var raw = await ChatAsync(system, user, ct).ConfigureAwait(false);
        var decision = TryParseDecision(JsonExtractor.ExtractJson(raw));
        if (decision is not null) return decision;

        _logger.LogWarning(
            "ExtensionsAiMemoryExtractor: failed to parse Consolidate response; retrying with strict prompt.");
        var raw2 = await ChatAsync(system, user + StrictJsonSuffix, ct).ConfigureAwait(false);
        var decision2 = TryParseDecision(JsonExtractor.ExtractJson(raw2));
        if (decision2 is not null) return decision2;

        _logger.LogError(
            "ExtensionsAiMemoryExtractor: retry also failed to parse Consolidate response; defaulting to Add.");
        return ConsolidationDecision.Add();
    }

    public async Task<string> SummarizeAsync(
        IReadOnlyList<MemoryEntry> memories, CancellationToken ct = default)
    {
        var facts  = string.Join("\n", memories.Select(m => $"- {m.Content}"));
        var prompt = $"Summarize these facts about a user into a single concise paragraph. Start with 'User background:'. Facts:\n{facts}";
        var raw    = await ChatAsync(system: null, prompt, ct).ConfigureAwait(false);
        return JsonExtractor.StripCodeFences(raw);
    }

    private async Task<string> ChatAsync(string? system, string userMessage, CancellationToken ct)
    {
        var messages = new List<ChatMessage>();
        if (system is not null) messages.Add(new ChatMessage(ChatRole.System, system));
        messages.Add(new ChatMessage(ChatRole.User, userMessage));

        var response = await _client.GetResponseAsync(messages, options: null, cancellationToken: ct)
                                    .ConfigureAwait(false);
        return response.Text?.Trim() ?? string.Empty;
    }

    private static List<string>? TryDeserializeList(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json, JsonOpts); }
        catch { return null; }
    }

    private static ConsolidationDecision? TryParseDecision(string json)
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
                _        => null
            };
        }
        catch { return null; }
    }
}

internal static class ExtractionPrompts
{
    public const string ExtractionSystem = """
        You are a memory extraction assistant. Extract discrete, useful facts about the user from conversations.

        Rules:
        - Extract ONLY facts about the USER — not the assistant's responses.
        - Each fact must be a single, self-contained sentence starting with "User".
        - Be specific and concrete.
        - Combine related details into one fact rather than splitting them.
        - Return a JSON array of strings. Example: ["User is a software engineer who loves C#."]
        - If no facts can be extracted, return: []
        - Return ONLY the JSON array — no explanation, no markdown, no code fences.
        """;

    public const string ConsolidationSystem = """
        You are a memory consolidation assistant. Given a new fact and a list of existing memories, decide what to do.

        Respond with ONLY one of these exact JSON formats:
        {"action":"ADD"} - use when the new fact is genuinely new.
        {"action":"NONE"} - use when the new fact is already covered.
        {"action":"UPDATE","targetId":"<id>","updatedContent":"<improved fact>"} - use to merge/improve an existing memory.
        {"action":"DELETE","targetId":"<id>"} - use when the new fact contradicts an existing memory.

        Priority: NONE > UPDATE > DELETE > ADD. Prefer doing less.
        Return ONLY the JSON object — no explanation, no markdown.
        """;
}

public static class ExtensionsAiExtractorExtensions
{
    /// <summary>
    /// Resolves an <see cref="IChatClient"/> from DI and registers
    /// <see cref="ExtensionsAiMemoryExtractor"/> as <see cref="IMemoryExtractor"/>.
    /// </summary>
    public static BlazorMemoryBuilder UseExtensionsAiExtractor(this BlazorMemoryBuilder builder)
    {
        builder.Services.AddScoped<IMemoryExtractor>(sp => new ExtensionsAiMemoryExtractor(
            sp.GetRequiredService<IChatClient>(),
            sp.GetService<ILogger<ExtensionsAiMemoryExtractor>>()));
        return builder;
    }

    /// <summary>
    /// Registers <see cref="ExtensionsAiMemoryExtractor"/> around a caller-supplied
    /// client instance. Useful when the client is not itself in DI.
    /// </summary>
    public static BlazorMemoryBuilder UseExtensionsAiExtractor(
        this BlazorMemoryBuilder builder, IChatClient client)
    {
        builder.Services.AddScoped<IMemoryExtractor>(sp => new ExtensionsAiMemoryExtractor(
            client, sp.GetService<ILogger<ExtensionsAiMemoryExtractor>>()));
        return builder;
    }
}
