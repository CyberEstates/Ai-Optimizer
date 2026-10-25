using System.Windows.Threading;
using AIGamingOptimizer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIGamingOptimizer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SystemMetricsService _metrics;
    private readonly DispatcherTimer _timer;

    [ObservableProperty] private string _section = "overview";
    [ObservableProperty] private object? _currentView;
    [ObservableProperty] private string _clock = DateTime.Now.ToString("HH:mm");
    [ObservableProperty] private bool _isElevated;

    public OverviewViewModel Overview { get; }
    public GameBoostViewModel GameBoost { get; }
    public CleanerViewModel Cleaner { get; }
    public StartupViewModel Startup { get; }
    public AssistantViewModel Assistant { get; }

    public MainViewModel(
        SystemMetricsService metrics,
        OverviewViewModel overview,
        GameBoostViewModel gameBoost,
        CleanerViewModel cleaner,
        StartupViewModel startup,
        AssistantViewModel assistant)
    {
        _metrics = metrics;
        Overview = overview;
        GameBoost = gameBoost;
        Cleaner = cleaner;
        Startup = startup;
        Assistant = assistant;
        CurrentView = overview;
        IsElevated = HardwareInfoService.IsElevated();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();
    }

    [RelayCommand]
    private void Navigate(string section)
    {
        Section = section;
        CurrentView = section switch
        {
            "boost" => GameBoost,
            "cleaner" => Cleaner,
            "startup" => Startup,
            "assistant" => Assistant,
            _ => Overview
        };

        if (CurrentView == GameBoost)
        {
            GameBoost.RefreshCommand.Execute(null);
        }

        if (CurrentView == Startup)
        {
            Startup.RefreshCommand.Execute(null);
        }
    }

    private void Tick()
    {
        Clock = DateTime.Now.ToString("HH:mm");
        try
        {
            var snap = _metrics.Capture();
            Overview.ApplySnapshot(snap);
            IsElevated = snap.IsElevated;
        }
        catch
        {
            // Keep the UI alive if a counter fails for one tick.
        }
    }
}
