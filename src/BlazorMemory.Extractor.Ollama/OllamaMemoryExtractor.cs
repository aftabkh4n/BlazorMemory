using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using Microsoft.Extensions.Options;

namespace BlazorMemory.Extractor.Ollama;

public sealed class OllamaMemoryExtractor : IMemoryExtractor
{
    private readonly HttpClient _http;
    private readonly OllamaExtractorOptions _options;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public OllamaMemoryExtractor(HttpClient http, IOptions<OllamaExtractorOptions> options)
    {
        _options = options.Value;
        _http = http;
        _http.BaseAddress ??= new Uri(_options.BaseUrl);
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
        raw = raw.Replace("```json", "").Replace("```", "").Trim();
        try { return JsonSerializer.Deserialize<List<string>>(raw, JsonOpts) ?? []; }
        catch { return []; }
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
        raw = raw.Replace("```json", "").Replace("```", "").Trim();
        return ParseDecision(raw);
    }

    public async Task<string> SummarizeAsync(
        IReadOnlyList<MemoryEntry> memories, CancellationToken ct = default)
    {
        var facts = string.Join("\n", memories.Select(m => $"- {m.Content}"));
        var prompt =
            "Summarize these facts about a user into a single concise paragraph.\n" +
            "Start with 'User background:'.\n" +
            $"Facts:\n{facts}";

        return await ChatAsync(prompt, ct);
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
                                node!["content"]!.GetValue<string>()),
                "DELETE" => ConsolidationDecision.Delete(
                                node!["targetId"]!.GetValue<string>()),
                _        => ConsolidationDecision.Add()
            };
        }
        catch { return ConsolidationDecision.Add(); }
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
