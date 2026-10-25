using System.Collections.ObjectModel;
using AIGamingOptimizer.Models;
using AIGamingOptimizer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIGamingOptimizer.ViewModels;

public partial class OverviewViewModel : ObservableObject
{
    private readonly SystemMetricsService _metrics;
    private readonly ProcessOptimizerService _processes;
    private readonly CleanupService _cleanup;
    private readonly PowerPlanService _power;
    private readonly StartupService _startup;

    [ObservableProperty] private SystemSnapshot _snapshot = new();
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private bool _busyAnalyze;
    [ObservableProperty] private bool _busyOptimize;

    public ObservableCollection<OptimizationModule> Modules { get; } = [];

    public OverviewViewModel(
        SystemMetricsService metrics,
        ProcessOptimizerService processes,
        CleanupService cleanup,
        PowerPlanService power,
        StartupService startup)
    {
        _metrics = metrics;
        _processes = processes;
        _cleanup = cleanup;
        _power = power;
        _startup = startup;

        Modules.Add(new OptimizationModule { Key = "bloat", Title = "Unnecessary Background Processes", Icon = "⚡", Stat = "—", Detail = "Safe-to-close apps competing for CPU/RAM" });
        Modules.Add(new OptimizationModule { Key = "temp", Title = "Temporary Files", Icon = "🧹", Stat = "—", Detail = "Windows temp, user temp, browser caches" });
        Modules.Add(new OptimizationModule { Key = "startup", Title = "Startup Items", Icon = "🚀", Stat = "—", Detail = "Boot-time apps that keep running idle" });
        Modules.Add(new OptimizationModule { Key = "gamemode", Title = "Game Mode Status", Icon = "🎮", Stat = "—", Detail = "Windows Game Mode + power plan" });
    }

    public void ApplySnapshot(SystemSnapshot snapshot)
    {
        Snapshot = snapshot;
        var bloat = Modules.First(m => m.Key == "bloat");
        bloat.Stat = $"{snapshot.SafeToCloseCount} apps";
        bloat.Detail = snapshot.SafeToCloseCount > 0
            ? "We've detected heavy background processes impacting gaming performance"
            : "Background load looks light";

        var game = Modules.First(m => m.Key == "gamemode");
        game.Stat = snapshot.GameModeEnabled ? "On" : "Off";
        game.Detail = snapshot.PowerPlan;
        game.IsEnabled = snapshot.GameModeEnabled;
    }

    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        if (IsBusy) return;
        IsBusy = BusyAnalyze = true;
        StatusText = "Analyzing system…";
        try
        {
            await Task.Run(() =>
            {
                var junk = _cleanup.EstimateJunkBytes();
                var startup = _startup.ListEntries();
                var snap = _metrics.Capture();
                App.Ui(() =>
                {
                    ApplySnapshot(snap);
                    Modules.First(m => m.Key == "temp").Stat = $"{junk / 1024d / 1024d:0} MB";
                    Modules.First(m => m.Key == "startup").Stat = $"{startup.Count(s => s.IsEnabled)} enabled";
                    StatusText = $"Health {snap.HealthScore}/100 · {snap.HealthSummary}";
                });
            });
        }
        finally
        {
            IsBusy = BusyAnalyze = false;
        }
    }

    [RelayCommand]
    private async Task OptimizeAsync()
    {
        if (IsBusy) return;
        IsBusy = BusyOptimize = true;
        StatusText = "Optimizing…";
        try
        {
            await Task.Run(() =>
            {
                var notes = new List<string>();
                if (Modules.First(m => m.Key == "bloat").IsEnabled)
                {
                    var boost = _processes.GameBoost();
                    notes.Add($"Closed {boost.Closed.Count} background apps");
                }

                if (Modules.First(m => m.Key == "temp").IsEnabled)
                {
                    var clean = _cleanup.CleanTempAndCaches();
                    notes.Add(clean.Summary);
                }

                if (Modules.First(m => m.Key == "gamemode").IsEnabled)
                {
                    notes.Add(_power.ApplyGamingPlan());
                }

                var snap = _metrics.Capture();
                App.Ui(() =>
                {
                    ApplySnapshot(snap);
                    StatusText = string.Join(" · ", notes);
                });
            });
        }
        finally
        {
            IsBusy = BusyOptimize = false;
        }
    }
}
