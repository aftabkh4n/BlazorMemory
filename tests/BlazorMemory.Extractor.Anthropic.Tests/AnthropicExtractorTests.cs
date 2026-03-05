using Xunit;
using BlazorMemory.Core.Extensions;
using BlazorMemory.Core.Models;
using BlazorMemory.Extractor.Anthropic;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorMemory.Extractor.Anthropic.Tests;

public class AnthropicExtractorTests
{
    private static AnthropicMemoryExtractor BuildExtractor(string apiKey = "test-key") =>
        new(Options.Create(new AnthropicExtractorOptions { ApiKey = apiKey }));

    // ── Option Validation Tests ────────────────────────────────────────────────

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
        var extractor = BuildExtractor();

        var decision = await extractor.ConsolidateAsync(
            "User is a software engineer",
            Array.Empty<MemoryEntry>());

        decision.Action.Should().Be(ConsolidationAction.Add);
    }

    // ── Default Options Tests ──────────────────────────────────────────────────

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

    // ── DI Registration Tests ──────────────────────────────────────────────────

    [Fact]
    public void UseAnthropicExtractor_RegistersExtractor_WithCorrectKey()
    {
        var services = new ServiceCollection();

        services.AddBlazorMemory().UseAnthropicExtractor("sk-ant-test-key");

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AnthropicExtractorOptions>>();

        options.Value.ApiKey.Should().Be("sk-ant-test-key");
        options.Value.Model.Should().Contain("haiku");
    }

    [Fact]
    public void UseAnthropicExtractor_WithSonnet_UsesCorrectModel()
    {
        var services = new ServiceCollection();

        services.AddBlazorMemory().UseAnthropicExtractor("sk-ant-test-key", model: "claude-sonnet-4-6");

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AnthropicExtractorOptions>>();

        options.Value.Model.Should().Be("claude-sonnet-4-6");
    }
}