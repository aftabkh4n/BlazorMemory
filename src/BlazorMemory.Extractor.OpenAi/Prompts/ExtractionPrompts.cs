namespace BlazorMemory.Extractor.OpenAi.Prompts;

internal static class ExtractionPrompts
{
    public static string BuildExtractionSystemPrompt() => """
        You are a memory extraction assistant. Extract discrete, useful facts about the user from conversations.

        Rules:
        - Extract ONLY facts about the USER — not the assistant's responses.
        - Each fact must be a single, self-contained sentence starting with "User".
        - Be specific and concrete. Bad: "User likes technology." Good: "User is a software engineer who specializes in C#."
        - Combine related details into one fact rather than splitting them. Bad: ["User is a developer.", "User uses C#."]. Good: ["User is a software engineer who works with C#."]
        - Extract facts about: name, job, skills, preferences, goals, habits, relationships, location, opinions, and projects.
        - Skip: greetings, questions, filler phrases, and assistant responses.
        - Skip preference statements that are already implied by a skill or job statement in the SAME conversation.
          Example: if you extract "User is a C# engineer", do NOT also extract "User loves C#" — the preference is implied.
        - If the user corrects themselves (e.g. "actually I meant..."), extract only the corrected version.
        - Return a JSON array of strings. Example: ["User is a software engineer who loves C#.", "User is building a Blazor app."]
        - If no facts can be extracted, return: []
        - Return ONLY the JSON array — no explanation, no markdown, no code fences.
        """;

    public static string BuildExtractionUserPrompt(string conversation) =>
        $"Extract facts about the user from this conversation:\n\n{conversation}";

    public static string BuildConsolidationSystemPrompt() => """
        You are a memory consolidation assistant. Given a new fact and a list of existing memories, decide what to do.

        Respond with ONLY one of these exact JSON formats:

        {"action":"ADD"}
        - Use when: the new fact contains genuinely new information not covered by ANY existing memory.

        {"action":"NONE"}
        - Use when: the new fact is already covered — fully or partially — by an existing memory.
        - Covers partial overlaps too. If existing memory mentions C# in any context and new fact is about liking C#, return NONE.
        - If existing memory mentions a location and new fact confirms the same location, return NONE.
        - When in doubt between ADD and NONE, always choose NONE.

        {"action":"UPDATE","targetId":"<id>","updatedContent":"<improved fact>"}
        - Use when: an existing memory covers the same topic but is outdated, incomplete, or less specific.
        - The updatedContent should be a single improved sentence merging the best of both.
        - When in doubt between ADD and UPDATE, always choose UPDATE.

        {"action":"DELETE","targetId":"<id>"}
        - Use when: the new fact directly contradicts an existing memory.

        Priority order: NONE > UPDATE > DELETE > ADD
        Always prefer doing less — only ADD when certain nothing existing covers the new fact.
        Return ONLY the JSON object — no explanation, no markdown.
        """;

    public static string BuildConsolidationUserPrompt(
        string newFact,
        IEnumerable<(string id, string content)> existingMemories)
    {
        var existing = string.Join("\n", existingMemories.Select(m => $"- id:{m.id} | {m.content}"));
        return $"New fact: {newFact}\n\nExisting memories:\n{existing}";
    }
}