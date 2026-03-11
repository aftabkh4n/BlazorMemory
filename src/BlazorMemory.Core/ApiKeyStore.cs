namespace BlazorMemory.Core;

/// <summary>
/// Process-wide singleton holding the API key entered by the user at runtime.
/// Providers read this at call time instead of the frozen IOptions snapshot.
/// </summary>
public sealed class ApiKeyStore
{
    public static readonly ApiKeyStore Instance = new();
    private ApiKeyStore() { }

    public string ApiKey { get; set; } = string.Empty;
    public bool HasKey => !string.IsNullOrWhiteSpace(ApiKey);
}