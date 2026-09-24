namespace BlazorMemory.Core.Models;

/// <summary>
/// Global options that control how <see cref="Services.MemoryEnabledChat"/> and
/// <see cref="Services.MemoryService.ChatWithMemoryAsync"/> dispatch extraction after
/// each turn. Registered as a singleton and mutated at startup via the fluent builder.
/// </summary>
public sealed class ExtractionOptions
{
    /// <summary>
    /// Selects the dispatch mode. Defaults to <see cref="ExtractionMode.Inline"/>.
    /// </summary>
    public ExtractionMode Mode { get; set; } = ExtractionMode.Inline;

    /// <summary>
    /// Maximum time inline extraction may take before it is cancelled and the reply
    /// is returned to the caller. Only used when <see cref="Mode"/> is
    /// <see cref="ExtractionMode.Inline"/>. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan ExtractionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of pending extraction items held in the channel when
    /// <see cref="Mode"/> is <see cref="ExtractionMode.Background"/>. When the
    /// channel is full, additional items are dropped and logged rather than
    /// blocking the chat request. Defaults to 256.
    /// </summary>
    public int BackgroundQueueCapacity { get; set; } = 256;
}
