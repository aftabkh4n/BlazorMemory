using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using Microsoft.Extensions.Logging;

namespace BlazorMemory.Core.Services;

public sealed class MemoryEnabledChat
{
    private readonly IMemoryService              _memory;
    private readonly ILogger<MemoryEnabledChat>  _logger;

    private const string DefaultBasePrompt =
        "You are a helpful, friendly assistant with persistent memory.\n" +
        "You remember things about the user from previous conversations.\n" +
        "Use memories naturally -- don't recite them verbatim, just let them inform your responses.\n" +
        "If you learn something new about the user, acknowledge it warmly.";

    public QueryOptions QueryOptions   { get; set; } = new();
    public string       BaseSystemPrompt { get; set; } = DefaultBasePrompt;

    public MemoryEnabledChat(IMemoryService memory, ILogger<MemoryEnabledChat> logger)
    {
        _memory = memory;
        _logger = logger;
    }

    public async Task<string> ChatAsync(
        string userMessage,
        string userId,
        Func<string, string, Task<string>> llmCall,
        string? @namespace = null,
        CancellationToken ct = default)
    {
        var opts = @namespace is null
            ? QueryOptions
            : QueryOptions with { Namespace = @namespace };

        var memories     = await _memory.QueryAsync(userMessage, userId, opts, ct);
        var systemPrompt = BuildSystemPrompt(memories);
        var reply        = await llmCall(systemPrompt, userMessage);

        await ExtractSafeAsync(userMessage, reply, userId, @namespace, ct);

        return reply;
    }

    private string BuildSystemPrompt(IReadOnlyList<MemoryEntry> memories)
    {
        if (memories.Count == 0) return BaseSystemPrompt;
        var memoryBlock = string.Join("\n", memories.Select(m => $"- {m.Content}"));
        return $"{BaseSystemPrompt}\n\nWhat you remember about this user:\n{memoryBlock}";
    }

    private async Task ExtractSafeAsync(
        string userMessage, string reply,
        string userId, string? @namespace, CancellationToken ct)
    {
        try
        {
            await _memory.ExtractAsync(
                $"User: {userMessage}\nAssistant: {reply}",
                userId, @namespace, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Memory extraction failed after chat.");
        }
    }
}
