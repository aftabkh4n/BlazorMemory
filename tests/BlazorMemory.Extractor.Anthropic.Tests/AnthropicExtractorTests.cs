using BlazorMemory.Core.Models;
using BlazorMemory.Extractor.Anthropic;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BlazorMemory.Extractor.Anthropic.Tests;

/// <summary>
/// Unit tests for AnthropicMemoryExtractor.
/// These test the decision parsing logic and option validation
/// without making real API calls.
/// </summary>
public class AnthropicExtractorTests
{
    private static AnthropicMemoryExtractor BuildExtractor(string apiKey = "test-key") =>
        new(Options.Create(new AnthropicExtractorOptions { ApiKey = apiKey }));

    private static MemoryEntry MakeMemory(string id, string content) => new()
    {
        Id        = id,
        UserId    = "user_1",
        Content   = content,
        Embedding = [0.1f, 0.2f],
        LearnedAt = DateTimeOffset.UtcNow
    };

    // ── Option Validation Tests ───────────────────────────────────────────────

    [Fact]
    public async Task ExtractFactsAsync_Throws_WhenApiKeyEmpty()
    {
        var extractor = BuildExtractor(string.Empty);

        var act = async () => await extractor.ExtractFactsAsync("Some conversation");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*API key*");
    }

    [Fact]
    public async Task ConsolidateAsync_ReturnsAdd_WhenNoSimilarMemories()
    {
        // When there are no similar memories, should always return Add
        // without making any API call
        var extractor = BuildExtractor();

        var decision = await extractor.ConsolidateAsync(
            "User is a software engineer",
            Array.Empty<MemoryEntry>());

        decision.Action.Should().Be(ConsolidationAction.Add);
    }

    // ── Default Options Tests ─────────────────────────────────────────────────

    [Fact]
    public void DefaultOptions_UseHaikuModel()
    {
        var options = new AnthropicExtractorOptions();
        options.Model.Should().Contain("haiku");
    }

    [Fact]
    public void DefaultOptions_ApiKey_IsEmpty()
    {
        var options = new AnthropicExtractorOptions();
        options.ApiKey.Should().BeEmpty();
    }

    [Fact]
    public void DefaultOptions_MaxTokens_IsSufficient()
    {
        var options = new AnthropicExtractorOptions();
        options.MaxTokens.Should().BeGreaterThanOrEqualTo(512);
    }

    // ── DI Registration Tests ─────────────────────────────────────────────────

    [Fact]
    public void UseAnthropicExtractor_RegistersExtractor_WithCorrectKey()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var builder = new BlazorMemory.Core.Extensions.BlazorMemoryBuilder(services);

        builder.UseAnthropicExtractor("sk-ant-test-key");

        var provider = services.BuildServiceProvider();
        var options  = provider.GetRequiredService<IOptions<AnthropicExtractorOptions>>();

        options.Value.ApiKey.Should().Be("sk-ant-test-key");
        options.Value.Model.Should().Contain("haiku");
    }

    [Fact]
    public void UseAnthropicExtractor_WithSonnet_UsesCorrectModel()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var builder  = new BlazorMemory.Core.Extensions.BlazorMemoryBuilder(services);

        builder.UseAnthropicExtractor("sk-ant-test-key", model: "claude-sonnet-4-6");

        var provider = services.BuildServiceProvider();
        var options  = provider.GetRequiredService<IOptions<AnthropicExtractorOptions>>();

        options.Value.Model.Should().Be("claude-sonnet-4-6");
    }
}
