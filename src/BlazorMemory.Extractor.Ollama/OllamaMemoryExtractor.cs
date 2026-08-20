using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BlazorMemory.Extractor.Ollama;

public sealed class OllamaMemoryExtractor : IMemoryExtractor
{
    private readonly HttpClient _http;
    private readonly OllamaExtractorOptions _options;
    private readonly ILogger<OllamaMemoryExtractor> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private const string StrictJsonSuffix =
        "\n\nIMPORTANT: Reply with ONLY the JSON. No markdown, no code fences, no explanation.";

    public OllamaMemoryExtractor(HttpClient http, IOptions<OllamaExtractorOptions> options,
        ILogger<OllamaMemoryExtractor>? logger = null)
    {
        _options = options.Value;
        _http = http;
        _http.BaseAddress ??= new Uri(_options.BaseUrl);
        _logger = logger ?? NullLogger<OllamaMemoryExtractor>.Instance;
    }

    public async Task<IReadOnlyList<string>> ExtractFactsAsync(
        string conversation, CancellationToken ct = default)
    {
        var prompt =
            "Extract discrete facts about the user from this conversation. " +
            "Return only a JSON array of strings. Each string is one fact starting with 'User'.\n" +
            "If there are no facts, return an empty array [].\n" +
            "Do not include explanations or markdown. Only the JSON array.\n" +
            $"Conversation:\n{conversation}";

        var raw = await ChatAsync(prompt, ct);
        var result = TryDeserializeList(ExtractJson(raw));
        if (result is not null) return result;

        _logger.LogWarning("OllamaMemoryExtractor: failed to parse ExtractFacts response; retrying with strict prompt.");
        var raw2 = await ChatAsync(prompt + StrictJsonSuffix, ct);
        var result2 = TryDeserializeList(ExtractJson(raw2));
        if (result2 is not null) return result2;

        _logger.LogError("OllamaMemoryExtractor: retry also failed to parse ExtractFacts response; returning empty list.");
        return [];
    }

    public async Task<ConsolidationDecision> ConsolidateAsync(
        string newFact, IReadOnlyList<MemoryEntry> similarMemories, CancellationToken ct = default)
    {
        if (similarMemories.Count == 0) return ConsolidationDecision.Add();

        var memoriesList = string.Join("\n", similarMemories.Select(m => $"id:{m.Id} content:{m.Content}"));
        var prompt =
            "You are a memory consolidation system. Given a new fact and similar existing memories,\n" +
            "decide what to do. Reply with exactly one of these JSON objects:\n" +
            "{\"action\":\"NONE\"} - if the fact is already covered\n" +
            "{\"action\":\"ADD\"} - if the fact is genuinely new\n" +
            "{\"action\":\"UPDATE\",\"targetId\":\"id\",\"content\":\"updated fact\"} - if an existing memory should be updated\n" +
            "{\"action\":\"DELETE\",\"targetId\":\"id\"} - if an existing memory contradicts and should be removed\n" +
            "Prefer NONE over ADD. Only ADD if the fact adds real new information.\n" +
            $"New fact: {newFact}\n" +
            $"Existing memories: {memoriesList}";

        var raw = await ChatAsync(prompt, ct);
        var decision = TryParseDecision(ExtractJson(raw));
        if (decision is not null) return decision;

        _logger.LogWarning("OllamaMemoryExtractor: failed to parse Consolidate response; retrying with strict prompt.");
        var raw2 = await ChatAsync(prompt + StrictJsonSuffix, ct);
        var decision2 = TryParseDecision(ExtractJson(raw2));
        if (decision2 is not null) return decision2;

        _logger.LogError("OllamaMemoryExtractor: retry also failed to parse Consolidate response; defaulting to Add.");
        return ConsolidationDecision.Add();
    }

    public async Task<string> SummarizeAsync(
        IReadOnlyList<MemoryEntry> memories, CancellationToken ct = default)
    {
        var facts = string.Join("\n", memories.Select(m => $"- {m.Content}"));
        var prompt =
            "Summarize these facts about a user into a single concise paragraph.\n" +
            "Start with 'User background:'.\n" +
            $"Facts:\n{facts}";

        var raw = await ChatAsync(prompt, ct);
        return StripCodeFences(raw);
    }

    private async Task<string> ChatAsync(string userPrompt, CancellationToken ct)
    {
        var request = new ChatRequest(
            _options.Model,
            [new OllamaChatMessage("user", userPrompt)],
            false);

        var response = await _http.PostAsJsonAsync("/api/chat", request, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(ct);
        return result?.Message?.Content?.Trim() ?? string.Empty;
    }

    internal static string ExtractJson(string raw)
    {
        var s = StripCodeFences(raw);

        var arrStart = s.IndexOf('[');
        var objStart = s.IndexOf('{');
        if (arrStart < 0 && objStart < 0) return s;

        int start; char close;
        if (arrStart < 0 || (objStart >= 0 && objStart < arrStart))
        { start = objStart; close = '}'; }
        else
        { start = arrStart; close = ']'; }

        var end = s.LastIndexOf(close);
        return end > start ? s[start..(end + 1)] : s;
    }

    private static string StripCodeFences(string raw)
    {
        var s = raw.Trim();
        if (!s.StartsWith("```")) return s;
        var nl = s.IndexOf('\n');
        if (nl >= 0) s = s[(nl + 1)..].Trim();
        var fence = s.LastIndexOf("```");
        if (fence >= 0) s = s[..fence].Trim();
        return s;
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
            var node = JsonNode.Parse(json);
            var action = node?["action"]?.GetValue<string>()?.ToUpperInvariant();
            return action switch
            {
                "ADD"    => ConsolidationDecision.Add(),
                "NONE"   => ConsolidationDecision.None(),
                "UPDATE" => ConsolidationDecision.Update(
                                node!["targetId"]!.GetValue<string>(),
                                node!["content"]!.GetValue<string>()),
                "DELETE" => ConsolidationDecision.Delete(
                                node!["targetId"]!.GetValue<string>()),
                _        => null
            };
        }
        catch { return null; }
    }

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")]    string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OllamaChatMessage> Messages,
        [property: JsonPropertyName("stream")]   bool Stream);

    private sealed record OllamaChatMessage(
        [property: JsonPropertyName("role")]    string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatResponse(
        [property: JsonPropertyName("message")] OllamaChatMessage? Message);
}
