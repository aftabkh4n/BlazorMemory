using BlazorMemory.Core;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Enums;
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

    public MemoryMode SelectedMemoryMode { get; set; } = MemoryMode.Semantic;
    public int QueryLimit { get; set; } = 5;
    public float SimilarityThreshold { get; set; } = 0.65f;
    public int? MaxAgeInDays { get; set; }
    public bool IncludeStalenessScore { get; set; }
    public int StalenessHalfLifeDays { get; set; } = 30;

    public async Task<string> SendAsync(
        string userMessage,
        IReadOnlyList<UserMessage> history,
        CancellationToken ct = default)
    {
        if (!HasApiKey)
            throw new InvalidOperationException(
                "OpenAI API key is not configured. Enter your key in the app config panel.");

        var memoryMode = SelectedMemoryMode;

        // 1. Retrieve relevant memories
        var memories = await GetRelevantMemoriesAsync(userMessage, memoryMode, ct);

        var systemPrompt = BuildSystemPrompt(memories);
        var client       = new OpenAIClient(ApiKeyStore.Instance.ApiKey).GetChatClient("gpt-4o-mini");
        var messages     = new List<ChatMessage> { new SystemChatMessage(systemPrompt) };

        foreach (var msg in history.TakeLast(20))
        {
            messages.Add(msg.Role == "user"
                ? new UserChatMessage(msg.Content)
                : new AssistantChatMessage(msg.Content));
        }
        messages.Add(new UserChatMessage(userMessage));

        // 2. Call OpenAI
        var response       = await client.CompleteChatAsync(messages, cancellationToken: ct);
        var assistantReply = response.Value.Content[0].Text;

        // 3. Extract memories in background
        _ = ExtractMemoriesAsync(userMessage, assistantReply, memoryMode);

        return assistantReply;
    }

    private async Task<IReadOnlyList<string>> GetRelevantMemoriesAsync(
        string userMessage,
        MemoryMode memoryMode,
        CancellationToken ct)
    {
        if (memoryMode == MemoryMode.Verbatim)
        {
            var verbatim = await _memory.SearchVerbatimAsync(
                UserId,
                userMessage,
                QueryLimit,
                ct);

            return verbatim.Select(m => m.Content).ToList();
        }

        var semantic = await _memory.QueryAsync(
            userMessage,
            UserId,
            new QueryOptions
            {
                Limit = QueryLimit,
                Threshold = SimilarityThreshold,
                MaxAgeInDays = MaxAgeInDays,
                IncludeStalenessScore = IncludeStalenessScore,
                StalenessHalfLifeDays = StalenessHalfLifeDays
            },
            ct);

        return semantic.Select(m => m.Content).ToList();
    }

    private static string BuildSystemPrompt(IReadOnlyList<string> memories)
    {
        var basePrompt = """
            You are a helpful, friendly assistant with persistent memory.
            You remember things about the user from previous conversations.
            Use memories naturally — don't recite them verbatim, just let them inform your responses.
            If you learn something new about the user, acknowledge it warmly.
            """;

        if (memories.Count == 0) return basePrompt;

        var memoryBlock = string.Join("\n", memories.Select(m => $"- {m}"));
        return $"{basePrompt}\n\nWhat you remember about this user:\n{memoryBlock}";
    }

    private async Task ExtractMemoriesAsync(
        string userMessage,
        string assistantReply,
        MemoryMode memoryMode)
    {
        try
        {
            var conversation = $"User: {userMessage}\nAssistant: {assistantReply}";
            if (memoryMode == MemoryMode.Verbatim)
                await _memory.StoreVerbatimAsync(UserId, conversation);
            else
                await _memory.ExtractAsync(conversation, UserId);
        }
        catch { /* Never crash the UI */ }
    }
}
