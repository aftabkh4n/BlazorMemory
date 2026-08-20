using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BlazorMemory.Extractor.AzureOpenAi;

public sealed class AzureOpenAiMemoryExtractor : IMemoryExtractor
{
    private readonly HttpClient _http;
    private readonly AzureOpenAiExtractorOptions _options;
    private readonly ILogger<AzureOpenAiMemoryExtractor> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private const string StrictJsonSuffix =
        "\n\nIMPORTANT: Reply with ONLY the JSON. No markdown, no code fences, no explanation.";

    public AzureOpenAiMemoryExtractor(HttpClient http, IOptions<AzureOpenAiExtractorOptions> options,
        ILogger<AzureOpenAiMemoryExtractor>? logger = null)
    {
        _options = options.Value;
        _http = http;
        _logger = logger ?? NullLogger<AzureOpenAiMemoryExtractor>.Instance;
    }

    private string ChatUrl =>
        $"{_options.Endpoint.TrimEnd('/')}/openai/deployments/{_options.DeploymentName}/chat/completions?api-version={_options.ApiVersion}";

    public async Task<IReadOnlyList<string>> ExtractFactsAsync(
        string conversation, CancellationToken ct = default)
    {
        const string system =
            "You are a memory extraction assistant. Extract discrete, useful facts about the user from conversations.\n" +
            "Each fact must be a single sentence starting with \"User\".\n" +
            "Return ONLY a JSON array of strings. Example: [\"User is a developer.\", \"User likes C#.\"]\n" +
            "If no facts can be extracted, return: []\n" +
            "Return ONLY the JSON array — no explanation, no markdown, no code fences.";

        var userContent = $"Extract facts about the user from this conversation:\n\n{conversation}";
        var raw = await ChatAsync(system, userContent, ct);
        var result = TryDeserializeList(ExtractJson(raw));
        if (result is not null) return result;

        _logger.LogWarning("AzureOpenAiMemoryExtractor: failed to parse ExtractFacts response; retrying with strict prompt.");
        var raw2 = await ChatAsync(system, userContent + StrictJsonSuffix, ct);
        var result2 = TryDeserializeList(ExtractJson(raw2));
        if (result2 is not null) return result2;

        _logger.LogError("AzureOpenAiMemoryExtractor: retry also failed to parse ExtractFacts response; returning empty list.");
        return [];
    }

    public async Task<ConsolidationDecision> ConsolidateAsync(
        string newFact, IReadOnlyList<MemoryEntry> similarMemories, CancellationToken ct = default)
    {
        if (similarMemories.Count == 0) return ConsolidationDecision.Add();

        const string system =
            "You are a memory consolidation assistant. Given a new fact and existing memories, decide what to do.\n" +
            "Respond with ONLY one of these JSON objects:\n" +
            "{\"action\":\"NONE\"} - fact already covered by existing memory\n" +
            "{\"action\":\"ADD\"} - genuinely new information\n" +
            "{\"action\":\"UPDATE\",\"targetId\":\"<id>\",\"updatedContent\":\"<improved fact>\"} - update existing\n" +
            "{\"action\":\"DELETE\",\"targetId\":\"<id>\"} - existing memory contradicts new fact\n" +
            "Priority: NONE > UPDATE > DELETE > ADD. Return ONLY the JSON — no explanation, no markdown.";

        var existing = string.Join("\n", similarMemories.Select(m => $"- id:{m.Id} | {m.Content}"));
        var userContent = $"New fact: {newFact}\n\nExisting memories:\n{existing}";

        var raw = await ChatAsync(system, userContent, ct);
        var decision = TryParseDecision(ExtractJson(raw));
        if (decision is not null) return decision;

        _logger.LogWarning("AzureOpenAiMemoryExtractor: failed to parse Consolidate response; retrying with strict prompt.");
        var raw2 = await ChatAsync(system, userContent + StrictJsonSuffix, ct);
        var decision2 = TryParseDecision(ExtractJson(raw2));
        if (decision2 is not null) return decision2;

        _logger.LogError("AzureOpenAiMemoryExtractor: retry also failed to parse Consolidate response; defaulting to Add.");
        return ConsolidationDecision.Add();
    }

    public async Task<string> SummarizeAsync(
        IReadOnlyList<MemoryEntry> memories, CancellationToken ct = default)
    {
        var facts = string.Join("\n", memories.Select(m => $"- {m.Content}"));
        var prompt = $"Summarize these facts about a user into a single concise paragraph. Start with 'User background:'. Facts:\n{facts}";
        var raw = await ChatAsync(null, prompt, ct);
        return StripCodeFences(raw);
    }

    private async Task<string> ChatAsync(string? system, string user, CancellationToken ct)
    {
        var messages = new List<ChatMessage>();
        if (system is not null) messages.Add(new ChatMessage("system", system));
        messages.Add(new ChatMessage("user", user));

        using var req = new HttpRequestMessage(HttpMethod.Post, ChatUrl);
        req.Headers.Add("api-key", _options.ApiKey);
        req.Content = JsonContent.Create(new ChatRequest(messages));

        var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOpts, ct);
        return result?.Choices?[0]?.Message?.Content?.Trim() ?? string.Empty;
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
                                node!["updatedContent"]!.GetValue<string>()),
                "DELETE" => ConsolidationDecision.Delete(
                                node!["targetId"]!.GetValue<string>()),
                _        => null
            };
        }
        catch { return null; }
    }

    private sealed record ChatRequest(
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")]    string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatChoice>? Choices);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatMessage? Message);
}
