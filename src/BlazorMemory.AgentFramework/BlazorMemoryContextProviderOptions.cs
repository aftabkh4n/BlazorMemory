using BlazorMemory.Core.Models;

namespace BlazorMemory.AgentFramework;

/// <summary>
/// Configuration for <see cref="BlazorMemoryContextProvider"/>.
/// </summary>
public sealed class BlazorMemoryContextProviderOptions
{
    /// <summary>
    /// REQUIRED. Resolves the current user id at each agent turn from the
    /// application's <see cref="IServiceProvider"/> (typically an
    /// <see cref="System.Security.Claims.ClaimsPrincipal"/> pulled from
    /// <c>IHttpContextAccessor</c>).
    /// <para>
    /// SECURITY: The user id must come from authenticated server-side context.
    /// Never derive it from client-supplied input (query string, headers,
    /// request body) or from an ambient value the caller can spoof — memories
    /// are scoped by user id, and a resolver that trusts client input leaks
    /// memories across users.
    /// </para>
    /// </summary>
    public Func<IServiceProvider, string>? UserIdResolver { get; set; }

    /// <summary>
    /// Optional namespace applied to both recall queries and extraction so an
    /// agent can partition its own memories from unrelated data for the same
    /// user (e.g. "coach", "assistant").
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Query options passed to <see cref="Core.Abstractions.IMemoryService.QueryAsync"/>
    /// when building the context to inject before each run.
    /// </summary>
    public QueryOptions QueryOptions { get; set; } = new();

    /// <summary>
    /// When true (default), the provider runs memory extraction against the
    /// request+response messages after each successful run.
    /// </summary>
    public bool ExtractAfterRun { get; set; } = true;
}
