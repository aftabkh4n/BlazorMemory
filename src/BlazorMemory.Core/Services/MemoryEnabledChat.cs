using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using Microsoft.Extensions.Logging;

namespace BlazorMemory.Core.Services;

public sealed class MemoryEnabledChat
{
    private readonly IMemoryService              _memory;
    private readonly ILogger<MemoryEnabledChat>  _logger;
    private readonly ExtractionOptions           _extractionOptions;
    private readonly IMemoryExtractionQueue?     _queue;

    private const string DefaultBasePrompt =
        "You are a helpful, friendly assistant with persistent memory.\n" +
        "You remember things about the user from previous conversations.\n" +
        "Use memories naturally -- don't recite them verbatim, just let them inform your responses.\n" +
        "If you learn something new about the user, acknowledge it warmly.";

    public QueryOptions QueryOptions   { get; set; } = new();
    public string       BaseSystemPrompt { get; set; } = DefaultBasePrompt;

    public MemoryEnabledChat(
        IMemoryService memory,
        ILogger<MemoryEnabledChat> logger,
        ExtractionOptions? extractionOptions = null,
        IMemoryExtractionQueue? queue = null)
    {
        _memory            = memory;
        _logger            = logger;
        _extractionOptions = extractionOptions ?? new ExtractionOptions();
        _queue             = queue;
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

        await ExtractionDispatcher.DispatchAsync(
            _extractionOptions,
            _queue,
            _memory,
            _logger,
            $"User: {userMessage}\nAssistant: {reply}",
            userId,
            @namespace,
            ct);

        return reply;
    }

    private string BuildSystemPrompt(IReadOnlyList<MemoryEntry> memories)
    {
        var memoryBlock = MemoryContextBuilder.Build(memories, "What you remember about this user:");
        return memoryBlock.Length == 0
            ? BaseSystemPrompt
            : $"{BaseSystemPrompt}\n\n{memoryBlock}";
    }
}
