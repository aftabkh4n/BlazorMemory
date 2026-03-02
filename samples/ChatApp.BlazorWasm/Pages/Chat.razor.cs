using BlazorMemory.Core.Models;
using ChatApp.BlazorWasm.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace ChatApp.BlazorWasm.Pages;

public partial class Chat
{
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
    private bool ApiKeySet { get; set; }

    private ElementReference MessagesRef;
    private ElementReference InputRef;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        await LoadMemoriesAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !ApiKeySet)
        {
            ShowConfig = true;
            StateHasChanged();
        }
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

            // Refresh memory panel after extraction
            await Task.Delay(1500); // give extraction a moment to complete
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
        // Send on Enter, new line on Shift+Enter
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await SendMessage();
        }
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
            // In a real app you'd store this securely — for demo we just set it on config
            ApiKeySet = true;
            ShowConfig = false;
            ErrorMessage = null;
            StateHasChanged();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatDate(DateTimeOffset dt)
    {
        var local = dt.ToLocalTime();
        var diff = DateTimeOffset.Now - dt;
        return diff.TotalMinutes < 1 ? "just now"
             : diff.TotalHours < 1 ? $"{(int)diff.TotalMinutes}m ago"
             : diff.TotalDays < 1 ? $"{(int)diff.TotalHours}h ago"
             : local.ToString("MMM d");
    }

    private async Task ScrollToBottomAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("scrollToBottom", MessagesRef);
        }
        catch { /* JS not ready yet */ }
    }

    private async Task FocusInputAsync()
    {
        try
        {
            await InputRef.FocusAsync();
        }
        catch { }
    }
}