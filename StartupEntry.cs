using CommunityToolkit.Mvvm.ComponentModel;

namespace AIGamingOptimizer.Models;

public partial class StartupEntry : ObservableObject
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required string Location { get; init; }
    public required string Hive { get; init; }

    [ObservableProperty]
    private bool _isEnabled;
}
