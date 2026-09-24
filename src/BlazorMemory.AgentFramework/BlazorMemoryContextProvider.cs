using BlazorMemory.Core.Abstractions;
using BlazorMemory.Core.Models;
using BlazorMemory.Core.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlazorMemory.AgentFramework;

/// <summary>
/// Microsoft Agent Framework <see cref="AIContextProvider"/> that plugs BlazorMemory
/// in as an agent's long-term memory. Before each run it recalls relevant memories for
/// the current user and injects them as additional instructions; after each run it
/// extracts new memories from the turn's user and assistant messages.
/// </summary>
public sealed class BlazorMemoryContextProvider : AIContextProvider
{
    private readonly IMemoryService                              _memory;
    private readonly BlazorMemoryContextProviderOptions          _options;
    private readonly ILogger<BlazorMemoryContextProvider>        _logger;
    private readonly IServiceProvider                            _serviceProvider;
    private readonly ExtractionOptions                           _extractionOptions;
    private readonly IMemoryExtractionQueue?                     _extractionQueue;

    public BlazorMemoryContextProvider(
        IMemoryService memory,
        BlazorMemoryContextProviderOptions options,
        IServiceProvider serviceProvider,
        ILogger<BlazorMemoryContextProvider>? logger = null,
        ExtractionOptions? extractionOptions = null,
        IMemoryExtractionQueue? extractionQueue = null)
        : base(static messages => messages, static messages => messages, static messages => messages)
    {
        if (options.UserIdResolver is null)
            throw new ArgumentException(
                $"{nameof(BlazorMemoryContextProviderOptions)}.{nameof(BlazorMemoryContextProviderOptions.UserIdResolver)} " +
                "is required. Provide a resolver that returns the authenticated user id from server-side context " +
                "(e.g. IHttpContextAccessor.HttpContext.User); never read the id from client-supplied input.",
                nameof(options));

        _memory            = memory;
        _options           = options;
        _logger            = logger ?? NullLogger<BlazorMemoryContextProvider>.Instance;
        _serviceProvider   = serviceProvider;
        _extractionOptions = extractionOptions ?? new ExtractionOptions();
        _extractionQueue   = extractionQueue;
    }

    protected override async ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var latestUserText = ExtractLatestUserText(context.AIContext.Messages);
            if (string.IsNullOrWhiteSpace(latestUserText))
                return new AIContext();

            var userId    = _options.UserIdResolver!(_serviceProvider);
            var opts      = _options.Namespace is null
                ? _options.QueryOptions
                : _options.QueryOptions with { Namespace = _options.Namespace };

            var memories  = await _memory.QueryAsync(latestUserText, userId, opts, cancellationToken)
                                         .ConfigureAwait(false);
            var block     = MemoryContextBuilder.Build(memories, "What you remember about this user:");
            return string.IsNullOrEmpty(block)
                ? new AIContext()
                : new AIContext { Instructions = block };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "BlazorMemoryContextProvider recall failed; agent run continues without memory context.");
            return new AIContext();
        }
    }

    protected override async ValueTask StoreAIContextAsync(
        InvokedContext context,
        CancellationToken cancellationToken)
    {
        if (!_options.ExtractAfterRun)
            return;

        if (context.InvokeException is not null)
            return;

        try
        {
            var userText   = FlattenText(context.RequestMessages,  ChatRole.User);
            var replyText  = FlattenText(context.ResponseMessages, ChatRole.Assistant);
            if (string.IsNullOrWhiteSpace(userText) && string.IsNullOrWhiteSpace(replyText))
                return;

            var conversation = $"User: {userText}\nAssistant: {replyText}";
            var userId       = _options.UserIdResolver!(_serviceProvider);

            await ExtractionDispatcher.DispatchAsync(
                _extractionOptions,
                _extractionQueue,
                _memory,
                _logger,
                conversation,
                userId,
                _options.Namespace,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "BlazorMemoryContextProvider extraction dispatch failed after agent run.");
        }
    }

    private static string? ExtractLatestUserText(IEnumerable<ChatMessage>? messages)
    {
        if (messages is null) return null;
        ChatMessage? latest = null;
        foreach (var m in messages)
            if (m.Role == ChatRole.User)
                latest = m;
        return latest?.Text;
    }

    private static string FlattenText(IEnumerable<ChatMessage>? messages, ChatRole role)
    {
        if (messages is null) return string.Empty;
        return string.Join("\n",
            messages.Where(m => m.Role == role).Select(m => m.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
    }
}
