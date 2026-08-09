namespace BlazorMemory.Embeddings.Ollama;

public sealed class OllamaEmbeddingsOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "nomic-embed-text";
    public int Dimensions { get; set; } = 768;
}
