using BlazorMemory.Components;
using ChatApp.BlazorWasm.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace ChatApp.BlazorWasm.Pages;

public partial class Chat
{
    [Inject] private ChatService ChatService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private List<UserMessage> Messages { get; set; } = [];
    private MemoryPanel? _memoryPanel;

    private string  InputText   { get; set; } = string.Empty;
    private string  ApiKeyInput { get; set; } = string.Empty;
    private string? ErrorMessage { get; set; }
    private string? CopiedId    { get; set; }

    private bool IsLoading       { get; set; }
    private bool ShowMemoryPanel { get; set; } = true;
    private bool ShowConfig      { get; set; }
    private bool ApiKeySet => ChatService.HasApiKey;

    private ElementReference MessagesRef;
    private ElementReference InputRef;

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !ApiKeySet)
        {
            ShowConfig = true;
            StateHasChanged();
        }
        return Task.CompletedTask;
    }

    private async Task SendMessage()
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text) || IsLoading || !ApiKeySet) return;

        InputText    = string.Empty;
        ErrorMessage = null;

        Messages.Add(new UserMessage { Role = "user", Content = text });
        IsLoading = true;
        StateHasChanged();
        await ScrollToBottomAsync();

        try
        {
            var reply = await ChatService.SendAsync(text, Messages);
            Messages.Add(new UserMessage { Role = "assistant", Content = reply });

            await Task.Delay(1500);
            if (_memoryPanel is not null)
                await _memoryPanel.RefreshAsync();
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

    private async Task CopyMessage(string content, string messageId)
    {
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", content);
        CopiedId = messageId;
        StateHasChanged();
        await Task.Delay(2000);
        CopiedId = null;
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
            ShowConfig   = false;
            ErrorMessage = null;
            StateHasChanged();
        }
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