namespace BlazorMemory.Extractor.Ollama;

public sealed class OllamaExtractorOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2";
}
