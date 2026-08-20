namespace BlazorMemory.Embeddings.AzureOpenAi;

public sealed class AzureOpenAiEmbeddingsOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2024-10-21";
    public int Dimensions { get; set; } = 1536;
}
