using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using ChatApp.BlazorWasm.Models;
using OpenAI;
using OpenAI.Chat;

namespace ChatApp.BlazorWasm;

/// <summary>
/// Orchestrates the chat loop:
/// 1. Inject relevant memories into the system prompt
/// 2. Send user message to OpenAI
/// 3. Extract new memories from the exchange in the background
/// </summary>
public sealed class ChatService
{
    private readonly IMemoryService _memory;
    private string _apiKey = string.Empty;

    private const string UserId = "demo_user";

    public ChatService(IMemoryService memory)
    {
        _memory = memory;
        // API key is set later via SetApiKey() from the UI
    }

    /// <summary>Called from the UI config panel when the user saves their API key.</summary>
    public void SetApiKey(string apiKey) => _apiKey = apiKey;

    public bool HasApiKey => !string.IsNullOrWhiteSpace(_apiKey);

    /// <summary>
    /// Sends a message and returns the assistant reply.
    /// Also triggers memory extraction in the background.
    /// </summary>
    public async Task<string> SendAsync(
        string userMessage,
        IReadOnlyList<UserMessage> history,
        CancellationToken ct = default)
    {
        if (!HasApiKey)
            throw new InvalidOperationException("Please enter your OpenAI API key first.");

        // 1. Retrieve relevant memories to inject into system prompt
        var memories = await _memory.QueryAsync(userMessage, UserId,
            new QueryOptions { Limit = 5, Threshold = 0.65f }, ct);

        var systemPrompt = BuildSystemPrompt(memories);

        // 2. Build message list for OpenAI
        var client = new OpenAIClient(_apiKey).GetChatClient("gpt-4o-mini");
        var messages = new List<OpenAI.Chat.ChatMessage> { new SystemChatMessage(systemPrompt) };

        foreach (var msg in history.TakeLast(20))
        {
            messages.Add(msg.Role == "user"
                ? new UserChatMessage(msg.Content)
                : new AssistantChatMessage(msg.Content));
        }
        messages.Add(new UserChatMessage(userMessage));

        // 3. Call OpenAI
        var response = await client.CompleteChatAsync(messages, cancellationToken: ct);
        var assistantReply = response.Value.Content[0].Text;

        // 4. Extract memories in background — pass key so providers can use it
        _ = ExtractMemoriesAsync(userMessage, assistantReply);

        return assistantReply;
    }

    /// <summary>Loads all stored memories for the demo user.</summary>
    public Task<IReadOnlyList<MemoryEntry>> GetAllMemoriesAsync(CancellationToken ct = default)
        => _memory.ListAsync(UserId, ct);

    /// <summary>Deletes a single memory by id.</summary>
    public Task DeleteMemoryAsync(string id) => _memory.DeleteAsync(id);

    /// <summary>Clears all memories for the demo user.</summary>
    public Task ClearAllMemoriesAsync() => _memory.ClearAsync(UserId);

    // ── Private ───────────────────────────────────────────────────────────────

    private static string BuildSystemPrompt(IReadOnlyList<MemoryEntry> memories)
    {
        var basePrompt = """
            You are a helpful, friendly assistant with persistent memory.
            You remember things about the user from previous conversations.
            Use memories naturally — don't recite them verbatim, just let them inform your responses.
            If you learn something new about the user, acknowledge it warmly.
            """;

        if (memories.Count == 0) return basePrompt;

        var memoryBlock = string.Join("\n", memories.Select(m => $"- {m.Content}"));
        return $"{basePrompt}\n\nWhat you remember about this user:\n{memoryBlock}";
    }

    private async Task ExtractMemoriesAsync(string userMessage, string assistantReply)
    {
        try
        {
            var conversation = $"User: {userMessage}\nAssistant: {assistantReply}";
            await _memory.ExtractAsync(conversation, UserId);
        }
        catch { /* Never crash the UI */ }
    }
}