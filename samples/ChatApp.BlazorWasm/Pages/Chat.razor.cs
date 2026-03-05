using BlazorMemory.Core.Models;
using ChatApp.BlazorWasm.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace ChatApp.BlazorWasm.Pages;

public partial class Chat
{
    [Inject] private ChatService ChatService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── State ─────────────────────────────────────────────────────────────────
    private List<UserMessage> Messages { get; set; } = [];
    private List<MemoryEntry> Memories { get; set; } = [];

    private string InputText { get; set; } = string.Empty;
    private string ApiKeyInput { get; set; } = string.Empty;
    private string? ErrorMessage { get; set; }

    private bool IsLoading { get; set; }
    private bool LoadingMemories { get; set; }
    private bool ShowMemoryPanel { get; set; } = true;
    private bool ShowConfig { get; set; }
    private bool ApiKeySet => ChatService.HasApiKey;

    private ElementReference MessagesRef;
    private ElementReference InputRef;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        await LoadMemoriesAsync();
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !ApiKeySet)
        {
            ShowConfig = true;
            StateHasChanged();
        }
        return Task.CompletedTask;
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private async Task SendMessage()
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text) || IsLoading || !ApiKeySet) return;

        InputText = string.Empty;
        ErrorMessage = null;

        Messages.Add(new UserMessage { Role = "user", Content = text });
        IsLoading = true;
        StateHasChanged();
        await ScrollToBottomAsync();

        try
        {
            var reply = await ChatService.SendAsync(text, Messages);
            Messages.Add(new UserMessage { Role = "assistant", Content = reply });

            // Refresh memory panel after a short delay for extraction to complete
            await Task.Delay(1500);
            await LoadMemoriesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Something went wrong: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
            await ScrollToBottomAsync();
            await FocusInputAsync();
        }
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
            await SendMessage();
    }

    private async Task DeleteMemory(string id)
    {
        await ChatService.DeleteMemoryAsync(id);
        await LoadMemoriesAsync();
    }

    private async Task ClearAllMemories()
    {
        await ChatService.ClearAllMemoriesAsync();
        await LoadMemoriesAsync();
    }

    private async Task LoadMemoriesAsync()
    {
        LoadingMemories = true;
        StateHasChanged();
        Memories = (await ChatService.GetAllMemoriesAsync()).ToList();
        LoadingMemories = false;
        StateHasChanged();
    }

    private void ToggleMemoryPanel() => ShowMemoryPanel = !ShowMemoryPanel;

    private void ToggleConfig()
    {
        ShowConfig = !ShowConfig;
        StateHasChanged();
    }

    private void SaveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(ApiKeyInput))
        {
            ChatService.SetApiKey(ApiKeyInput.Trim());
            ShowConfig = false;
            ErrorMessage = null;
            StateHasChanged();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatDate(DateTimeOffset dt)
    {
        var diff = DateTimeOffset.UtcNow - dt;
        return diff.TotalMinutes < 1 ? "just now"
             : diff.TotalHours < 1 ? $"{(int)diff.TotalMinutes}m ago"
             : diff.TotalDays < 1 ? $"{(int)diff.TotalHours}h ago"
             : dt.ToLocalTime().ToString("MMM d");
    }

    private async Task ScrollToBottomAsync()
    {
        try { await JS.InvokeVoidAsync("scrollToBottom", MessagesRef); }
        catch { }
    }

    private async Task FocusInputAsync()
    {
        try { await InputRef.FocusAsync(); }
        catch { }
    }
}