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
    private readonly ChatClient _chatClient;

    private const string UserId = "demo_user";

    public ChatService(IMemoryService memory, IConfiguration config)
    {
        _memory = memory;
        var apiKey = config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey not configured.");
        _chatClient = new OpenAIClient(apiKey).GetChatClient("gpt-4o-mini");
    }

    /// <summary>
    /// Sends a message and returns the assistant reply.
    /// Also triggers memory extraction in the background (fire and forget).
    /// </summary>
    public async Task<string> SendAsync(
        string userMessage,
        IReadOnlyList<UserMessage> history,
        CancellationToken ct = default)
    {
        // 1. Retrieve relevant memories to inject into system prompt
        var memories = await _memory.QueryAsync(userMessage, UserId,
            new QueryOptions { Limit = 5, Threshold = 0.65f }, ct);

        var systemPrompt = BuildSystemPrompt(memories);

        // 2. Build message list for OpenAI — fully qualified to avoid any ambiguity
        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage(systemPrompt)
        };

        foreach (var msg in history.TakeLast(20))
        {
            messages.Add(msg.Role == "user"
                ? new UserChatMessage(msg.Content)
                : new AssistantChatMessage(msg.Content));
        }
        messages.Add(new UserChatMessage(userMessage));

        // 3. Call OpenAI
        var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: ct);
        var assistantReply = response.Value.Content[0].Text;

        // 4. Extract memories in background (don't block the UI)
        _ = ExtractMemoriesAsync(userMessage, assistantReply);

        return assistantReply;
    }

    /// <summary>Loads all stored memories for the demo user.</summary>
    public Task<IReadOnlyList<MemoryEntry>> GetAllMemoriesAsync(CancellationToken ct = default)
        => _memory.ListAsync(UserId, ct);

    /// <summary>Deletes a single memory by id.</summary>
    public Task DeleteMemoryAsync(string id)
        => _memory.DeleteAsync(id);

    /// <summary>Clears all memories for the demo user.</summary>
    public Task ClearAllMemoriesAsync()
        => _memory.ClearAsync(UserId);

    // ── Private ───────────────────────────────────────────────────────────────

    private static string BuildSystemPrompt(IReadOnlyList<MemoryEntry> memories)
    {
        var basePrompt = """
            You are a helpful, friendly assistant with persistent memory.
            You remember things about the user from previous conversations.
            Use memories naturally — don't recite them verbatim, just let them inform your responses.
            If you learn something new about the user, acknowledge it warmly.
            """;

        if (memories.Count == 0)
            return basePrompt;

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
        catch
        {
            // Silently swallow — memory extraction should never crash the chat UI
        }
    }
}