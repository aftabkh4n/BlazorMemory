namespace BlazorMemory.Extractor.OpenAi.Prompts;

internal static class ExtractionPrompts
{
    public static string BuildExtractionSystemPrompt() => """
        You are a memory extraction assistant. Your job is to extract discrete, self-contained facts about the user from a conversation.

        Rules:
        - Extract only facts about the USER, not the assistant.
        - Each fact must be a single, concise sentence.
        - Facts must be objective and specific (avoid vague statements like "user seems nice").
        - Do not extract greetings, questions, or conversational filler.
        - Return facts as a JSON array of strings. Example: ["User is a software engineer.", "User works at Acme Corp."]
        - If no facts can be extracted, return an empty array: []
        - Return ONLY the JSON array, no explanation or markdown.
        """;

    public static string BuildExtractionUserPrompt(string conversation) =>
        $"Extract facts from this conversation:\n\n{conversation}";

    public static string BuildConsolidationSystemPrompt() => """
        You are a memory consolidation assistant. You are given a new fact and a list of existing memories.
        Your job is to decide what to do with the new fact.

        Respond with ONLY a JSON object in one of these exact formats:

        {"action":"ADD"}
        {"action":"NONE"}
        {"action":"UPDATE","targetId":"<id>","updatedContent":"<merged fact string>"}
        {"action":"DELETE","targetId":"<id>"}

        Decision rules:
        - ADD: No existing memory covers this fact. Store it as new.
        - NONE: An existing memory already captures this fact accurately. Do nothing.
        - UPDATE: An existing memory is related but outdated or incomplete. Merge into a single improved fact.
        - DELETE: The new fact explicitly contradicts and replaces an existing memory.

        Return ONLY the JSON object. No explanation, no markdown.
        """;

    public static string BuildConsolidationUserPrompt(
        string newFact,
        IEnumerable<(string id, string content)> existingMemories)
    {
        var existing = string.Join("\n", existingMemories.Select(m => $"- id: {m.id} | content: {m.content}"));
        return $"New fact: {newFact}\n\nExisting memories:\n{existing}";
    }
}
