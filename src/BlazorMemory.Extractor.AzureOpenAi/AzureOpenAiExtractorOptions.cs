namespace BlazorMemory.Extractor.AzureOpenAi;

public sealed class AzureOpenAiExtractorOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2024-10-21";
}
