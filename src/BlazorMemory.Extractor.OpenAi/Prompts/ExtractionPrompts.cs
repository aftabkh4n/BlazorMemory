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
        - Skip: greetings, questions, filler phrases, assistant responses, and anything not factual about the user.
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
        - Use when: the new fact is genuinely new information not covered by any existing memory.

        {"action":"NONE"}
        - Use when: an existing memory already captures this fact accurately. Prefer NONE over ADD for duplicates.

        {"action":"UPDATE","targetId":"<id>","updatedContent":"<improved fact>"}
        - Use when: an existing memory covers the same topic but is outdated, incomplete, or less specific.
        - The updatedContent should be a single improved sentence that merges the best of both.
        - Example: existing "User is a developer." + new "User is a senior C# developer at Microsoft." → UPDATE with "User is a senior C# developer at Microsoft."

        {"action":"DELETE","targetId":"<id>"}
        - Use when: the new fact directly contradicts an existing memory and the old one should be removed entirely.
        - Example: existing "User lives in London." + new "User moved to New York." → DELETE the London memory, then the new fact will be ADDed separately.

        Important:
        - When in doubt between ADD and NONE, choose NONE to avoid storing redundant facts.
        - When in doubt between ADD and UPDATE, choose UPDATE to keep memories consolidated.
        - Never UPDATE with less specific information than what already exists.
        - Return ONLY the JSON object — no explanation, no markdown.
        """;

    public static string BuildConsolidationUserPrompt(
        string newFact,
        IEnumerable<(string id, string content)> existingMemories)
    {
        var existing = string.Join("\n", existingMemories.Select(m => $"- id:{m.id} | {m.content}"));
        return $"New fact: {newFact}\n\nExisting memories:\n{existing}";
    }
}