using System.Collections.ObjectModel;
using AIGamingOptimizer.Models;
using AIGamingOptimizer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIGamingOptimizer.ViewModels;

public partial class AssistantViewModel : ObservableObject
{
    private readonly AiAssistantService _ai;

    [ObservableProperty] private string _draft = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _hint = "Ask about CPU, GPU, stuttering, or say “Boost my FPS now”.";

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public AssistantViewModel(AiAssistantService ai)
    {
        _ai = ai;
        Messages.Add(new ChatMessage
        {
            Role = "assistant",
            Text = "I'm your on-device gaming optimizer. I can read live CPU/GPU/RAM, close background bloat, and walk you through why the PC might be stuttering."
        });
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var text = Draft.Trim();
        if (string.IsNullOrWhiteSpace(text) || IsBusy)
        {
            return;
        }

        Draft = string.Empty;
        Messages.Add(new ChatMessage { Role = "user", Text = text });
        IsBusy = true;
        Hint = "Querying live system data…";
        try
        {
            var reply = await _ai.ChatAsync(text);
            Messages.Add(new ChatMessage { Role = "assistant", Text = reply });
            Hint = "Ask a follow-up, or tell me what to optimize.";
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                Role = "assistant",
                Text = "I couldn't reach the AI API. Check BaseUrl/Model in appsettings.json (or AI_BASE_URL / AI_API_KEY). " +
                       $"Details: {ex.Message}"
            });
            Hint = "API error — see the message above.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Quick(string prompt)
    {
        Draft = prompt;
        _ = SendAsync();
    }
}
