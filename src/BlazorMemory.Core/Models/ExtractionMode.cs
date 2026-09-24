namespace BlazorMemory.Core.Models;

/// <summary>
/// Controls how memory extraction runs after a chat turn.
/// </summary>
public enum ExtractionMode
{
    /// <summary>
    /// Extraction runs on the same request pipeline, wrapped in a timeout so
    /// a slow LLM or storage call cannot indefinitely delay the caller.
    /// Timeouts or failures are logged and the caller still receives the reply.
    /// This is the default and the only mode supported in Blazor WebAssembly.
    /// </summary>
    Inline,

    /// <summary>
    /// Extraction is enqueued to an in-process channel and drained by a
    /// background hosted service. Chat turns return immediately. Requires
    /// a host running <see cref="Microsoft.Extensions.Hosting.IHostedService"/>
    /// implementations (ASP.NET Core, worker services, .NET generic host).
    /// Not supported in Blazor WebAssembly.
    /// </summary>
    Background
}
