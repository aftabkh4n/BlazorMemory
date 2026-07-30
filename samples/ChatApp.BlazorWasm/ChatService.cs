using BlazorMemory.Core;
using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Enums;
using BlazorMemory.Core.Models;
using BlazorMemory.Core.Services;
using ChatApp.BlazorWasm.Models;
using OpenAI;
using OpenAI.Chat;

namespace ChatApp.BlazorWasm;

public sealed class ChatService
{
    private readonly IMemoryService  _memory;
    private readonly MemoryEnabledChat _memoryChat;
    private const string UserId = "demo_user";

    public ChatService(IMemoryService memory, MemoryEnabledChat memoryChat)
    {
        _memory     = memory;
        _memoryChat = memoryChat;
    }

    public void SetApiKey(string apiKey)
    {
        ApiKeyStore.Instance.ApiKey = apiKey;
    }

    public bool HasApiKey => ApiKeyStore.Instance.HasKey;

    public MemoryMode SelectedMemoryMode  { get; set; } = MemoryMode.Semantic;
    public int        QueryLimit          { get; set; } = 5;
    public float      SimilarityThreshold { get; set; } = 0.65f;
    public int?       MaxAgeInDays        { get; set; }
    public bool       IncludeStalenessScore { get; set; }
    public int        StalenessHalfLifeDays { get; set; } = 30;

    public async Task<string> SendAsync(
        string userMessage,
        IReadOnlyList<UserMessage> history,
        CancellationToken ct = default)
    {
        if (!HasApiKey)
            throw new InvalidOperationException(
                "OpenAI API key is not configured. Enter your key in the app config panel.");

        if (SelectedMemoryMode == MemoryMode.Verbatim)
            return await SendVerbatimAsync(userMessage, history, ct);

        return await SendSemanticAsync(userMessage, history, ct);
    }

    private async Task<string> SendSemanticAsync(
        string userMessage,
        IReadOnlyList<UserMessage> history,
        CancellationToken ct)
    {
        _memoryChat.QueryOptions = new QueryOptions
        {
            Limit                 = QueryLimit,
            Threshold             = SimilarityThreshold,
            MaxAgeInDays          = MaxAgeInDays,
            IncludeStalenessScore = IncludeStalenessScore,
            StalenessHalfLifeDays = StalenessHalfLifeDays
        };

        return await _memoryChat.ChatAsync(
            userMessage,
            UserId,
            (systemPrompt, msg) => CallOpenAiAsync(systemPrompt, msg, history, ct),
            ct: ct);
    }

    private async Task<string> SendVerbatimAsync(
        string userMessage,
        IReadOnlyList<UserMessage> history,
        CancellationToken ct)
    {
        var verbatim = await _memory.SearchVerbatimAsync(UserId, userMessage, QueryLimit, ct);
        var systemPrompt = BuildSystemPrompt(verbatim.Select(m => m.Content).ToList());
        var reply = await CallOpenAiAsync(systemPrompt, userMessage, history, ct);
        _ = TryStoreVerbatimAsync(userMessage, reply);
        return reply;
    }

    private static async Task<string> CallOpenAiAsync(
        string systemPrompt,
        string userMessage,
        IReadOnlyList<UserMessage> history,
        CancellationToken ct)
    {
        var client   = new OpenAIClient(ApiKeyStore.Instance.ApiKey).GetChatClient("gpt-4o-mini");
        var messages = new List<ChatMessage> { new SystemChatMessage(systemPrompt) };

        foreach (var msg in history.TakeLast(20))
        {
            messages.Add(msg.Role == "user"
                ? new UserChatMessage(msg.Content)
                : new AssistantChatMessage(msg.Content));
        }
        messages.Add(new UserChatMessage(userMessage));

        var response = await client.CompleteChatAsync(messages, cancellationToken: ct);
        return response.Value.Content[0].Text;
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

    private async Task TryStoreVerbatimAsync(string userMessage, string reply)
    {
        try
        {
            await _memory.StoreVerbatimAsync(UserId, $"User: {userMessage}\nAssistant: {reply}");
        }
        catch { /* Never crash the UI */ }
    }
}
