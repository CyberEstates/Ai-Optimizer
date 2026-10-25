using CommunityToolkit.Mvvm.ComponentModel;

namespace AIGamingOptimizer.Models;

public partial class OptimizationModule : ObservableObject
{
    public required string Title { get; init; }
    public required string Icon { get; init; }
    public required string Key { get; init; }

    [ObservableProperty]
    private string _stat = string.Empty;

    [ObservableProperty]
    private string _detail = string.Empty;

    [ObservableProperty]
    private bool _isEnabled = true;
}
