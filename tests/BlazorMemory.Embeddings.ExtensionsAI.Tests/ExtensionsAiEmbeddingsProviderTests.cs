using BlazorMemory.Embeddings.ExtensionsAI;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace BlazorMemory.Embeddings.ExtensionsAI.Tests;

public class ExtensionsAiEmbeddingsProviderTests
{
    private sealed class FakeGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public EmbeddingGeneratorMetadata? Metadata { get; init; }
        public Func<IEnumerable<string>, IReadOnlyList<Embedding<float>>>? OnGenerate { get; init; }

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var vectors = OnGenerate is null
                ? values.Select(_ => new Embedding<float>(new float[] { 0.1f, 0.2f, 0.3f })).ToList()
                : OnGenerate(values).ToList();
            var result = new GeneratedEmbeddings<Embedding<float>>();
            foreach (var v in vectors) result.Add(v);
            return Task.FromResult(result);
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceType == typeof(EmbeddingGeneratorMetadata) ? Metadata : null;

        public void Dispose() { }
    }

    [Fact]
    public async Task EmbedAsync_Returns_Vector_From_Generator()
    {
        var generator = new FakeGenerator
        {
            Metadata = new EmbeddingGeneratorMetadata("openai", null, "text-embedding-3-small", 3)
        };
        var sut = new ExtensionsAiEmbeddingsProvider(generator);

        var vec = await sut.EmbedAsync("hello");

        vec.Should().Equal(new[] { 0.1f, 0.2f, 0.3f });
    }

    [Fact]
    public void ModelIdentifier_Uses_ProviderName_And_DefaultModelId()
    {
        var generator = new FakeGenerator
        {
            Metadata = new EmbeddingGeneratorMetadata("openai", null, "text-embedding-3-small", 1536)
        };
        var sut = new ExtensionsAiEmbeddingsProvider(generator);

        sut.ModelIdentifier.Should().Be("openai/text-embedding-3-small");
        sut.Dimensions.Should().Be(1536);
    }

    [Fact]
    public void ModelIdentifier_Falls_Back_When_No_Metadata()
    {
        var generator = new FakeGenerator { Metadata = null };
        var sut = new ExtensionsAiEmbeddingsProvider(generator, new ExtensionsAiEmbeddingsOptions { Dimensions = 8 });

        sut.ModelIdentifier.Should().Be("extensionsai/unknown");
        sut.Dimensions.Should().Be(8);
    }

    [Fact]
    public void Ctor_Throws_When_No_Dimensions_Available()
    {
        var generator = new FakeGenerator
        {
            Metadata = new EmbeddingGeneratorMetadata("openai", null, "some-model", null)
        };
        var act = () => new ExtensionsAiEmbeddingsProvider(generator);
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Dimensions*");
    }
}
