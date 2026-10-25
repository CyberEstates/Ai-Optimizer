using CommunityToolkit.Mvvm.ComponentModel;

namespace AIGamingOptimizer.Models;

public partial class ChatMessage : ObservableObject
{
    public required string Role { get; init; }

    [ObservableProperty]
    private string _text = string.Empty;

    public bool IsUser => Role.Equals("user", StringComparison.OrdinalIgnoreCase);
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
}
