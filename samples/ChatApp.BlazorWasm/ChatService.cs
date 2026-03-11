using BlazorMemory.Core;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using ChatApp.BlazorWasm.Models;
using OpenAI;
using OpenAI.Chat;

namespace ChatApp.BlazorWasm;

public sealed class ChatService
{
    private readonly IMemoryService _memory;
    private const string UserId = "demo_user";

    public ChatService(IMemoryService memory)
    {
        _memory = memory;
    }

    public void SetApiKey(string apiKey)
    {
        ApiKeyStore.Instance.ApiKey = apiKey;
    }

    public bool HasApiKey => ApiKeyStore.Instance.HasKey;

    public async Task<string> SendAsync(
        string userMessage,
        IReadOnlyList<UserMessage> history,
        CancellationToken ct = default)
    {
        if (!HasApiKey)
            throw new InvalidOperationException(
                "OpenAI API key is not configured. Enter your key in the app config panel.");

        var memories = await _memory.QueryAsync(userMessage, UserId,
            new QueryOptions { Limit = 5, Threshold = 0.65f }, ct);

        var systemPrompt = BuildSystemPrompt(memories);
        var client = new OpenAIClient(ApiKeyStore.Instance.ApiKey).GetChatClient("gpt-4o-mini");
        var messages = new List<ChatMessage> { new SystemChatMessage(systemPrompt) };

        foreach (var msg in history.TakeLast(20))
        {
            messages.Add(msg.Role == "user"
                ? new UserChatMessage(msg.Content)
                : new AssistantChatMessage(msg.Content));
        }
        messages.Add(new UserChatMessage(userMessage));

        var response = await client.CompleteChatAsync(messages, cancellationToken: ct);
        var assistantReply = response.Value.Content[0].Text;

        _ = ExtractMemoriesAsync(userMessage, assistantReply);

        return assistantReply;
    }

    public Task<IReadOnlyList<MemoryEntry>> GetAllMemoriesAsync(CancellationToken ct = default)
        => _memory.ListAsync(UserId, ct);

    public Task DeleteMemoryAsync(string id) => _memory.DeleteAsync(id);
    public Task ClearAllMemoriesAsync() => _memory.ClearAsync(UserId);

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