using System.Collections.ObjectModel;
using AIGamingOptimizer.Models;
using AIGamingOptimizer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIGamingOptimizer.ViewModels;

public partial class GameBoostViewModel : ObservableObject
{
    private readonly ProcessOptimizerService _processes;
    private readonly PowerPlanService _power;

    [ObservableProperty] private string _filter = "Safe";
    [ObservableProperty] private string _status = "Select background apps, then boost.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _safeCount;

    public ObservableCollection<ProcessRow> Processes { get; } = [];

    public GameBoostViewModel(ProcessOptimizerService processes, PowerPlanService power)
    {
        _processes = processes;
        _power = power;
    }

    [RelayCommand]
    private void Refresh()
    {
        var scan = _processes.Scan();
        SafeCount = scan.Count(p => p.Category == ProcessCategory.SafeToClose);
        IEnumerable<ProcessInfoItem> view = Filter switch
        {
            "All" => scan,
            "Critical" => scan.Where(p => p.Category == ProcessCategory.Critical),
            _ => scan.Where(p => p.Category != ProcessCategory.Critical)
        };

        Processes.Clear();
        foreach (var item in view.Take(80))
        {
            Processes.Add(new ProcessRow(item));
        }
    }

    [RelayCommand]
    private async Task BoostSelectedAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Status = "Applying Game Boost…";
        try
        {
            var ids = Processes.Where(p => p.IsSelected && p.Category != ProcessCategory.Critical)
                .Select(p => p.Pid)
                .ToList();
            var result = await Task.Run(() =>
            {
                var boost = _processes.GameBoost(ids);
                var power = _power.ApplyGamingPlan();
                return (boost, power);
            });
            Status = $"Closed {result.boost.Closed.Count} · {result.boost.BoostedApp ?? "no game detected"} · {result.power}";
            Refresh();
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public partial class ProcessRow : ObservableObject
{
    public ProcessRow(ProcessInfoItem item)
    {
        Pid = item.Pid;
        Name = item.Name;
        Title = string.IsNullOrWhiteSpace(item.Title) ? "—" : item.Title;
        Cpu = $"{item.CpuPercent:0.0}%";
        Memory = $"{item.MemoryMb:0} MB";
        Category = item.Category;
        CategoryLabel = item.Category switch
        {
            ProcessCategory.Critical => "Critical",
            ProcessCategory.SafeToClose => "Safe to close",
            _ => "User app"
        };
        IsSelected = item.IsSelected;
    }

    public int Pid { get; }
    public string Name { get; }
    public string Title { get; }
    public string Cpu { get; }
    public string Memory { get; }
    public ProcessCategory Category { get; }
    public string CategoryLabel { get; }

    [ObservableProperty]
    private bool _isSelected;
}
