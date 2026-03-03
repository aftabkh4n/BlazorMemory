namespace BlazorMemory.Extractor.Anthropic.Prompts;

/// <summary>
/// Prompt templates for Claude-based fact extraction and consolidation.
/// Claude's instruction-following and JSON output is excellent for this task —
/// the prompts are tuned to get clean, parseable responses.
/// </summary>
internal static class ExtractionPrompts
{
    public static string BuildExtractionSystemPrompt() => """
        You are a memory extraction assistant. Extract discrete, self-contained facts about the USER from conversations.

        Rules:
        - Extract only facts about the USER, not the assistant.
        - Each fact must be a single, concise sentence in third person (e.g. "User works at Acme Corp.").
        - Facts must be specific and objective. Avoid vague statements.
        - Do not extract greetings, questions, or filler.
        - Return ONLY a JSON array of strings. Example: ["User is a software engineer.", "User works at Acme Corp."]
        - If no facts can be extracted, return: []
        - No explanation, no markdown, no code fences. Just the JSON array.
        """;

    public static string BuildExtractionUserPrompt(string conversation) =>
        $"Extract facts from this conversation:\n\n{conversation}";

    public static string BuildConsolidationSystemPrompt() => """
        You are a memory consolidation assistant. Given a new fact and existing memories, decide what to do.

        Respond with ONLY one of these exact JSON formats — no explanation, no markdown:

        {"action":"ADD"}
        {"action":"NONE"}
        {"action":"UPDATE","targetId":"<id>","updatedContent":"<merged fact>"}
        {"action":"DELETE","targetId":"<id>"}

        Decision rules:
        - ADD: No existing memory covers this fact. Store it as new.
        - NONE: An existing memory already captures this fact accurately. Do nothing.
        - UPDATE: An existing memory is related but outdated or incomplete. Provide a single improved merged fact.
        - DELETE: The new fact explicitly contradicts and invalidates an existing memory.

        Be precise. Prefer UPDATE over ADD when a related memory exists.
        """;

    public static string BuildConsolidationUserPrompt(
        string newFact,
        IEnumerable<(string id, string content)> existingMemories)
    {
        var existing = string.Join("\n", existingMemories.Select(m => $"- id: {m.id} | content: {m.content}"));
        return $"New fact: {newFact}\n\nExisting memories:\n{existing}";
    }
}
