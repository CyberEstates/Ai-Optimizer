using System.Collections.ObjectModel;
using AIGamingOptimizer.Models;
using AIGamingOptimizer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIGamingOptimizer.ViewModels;

public partial class StartupViewModel : ObservableObject
{
    private readonly StartupService _startup;
    private readonly PowerPlanService _power;

    [ObservableProperty] private string _status = "Disable non-essential startup apps to cut idle load.";
    [ObservableProperty] private string _powerPlan = "Unknown";

    public ObservableCollection<StartupEntry> Entries { get; } = [];

    public StartupViewModel(StartupService startup, PowerPlanService power)
    {
        _startup = startup;
        _power = power;
    }

    [RelayCommand]
    private void Refresh()
    {
        Entries.Clear();
        foreach (var entry in _startup.ListEntries())
        {
            Entries.Add(entry);
        }

        PowerPlan = _power.GetActivePlanName();
        Status = $"{Entries.Count(e => e.IsEnabled)} startup items enabled";
    }

    [RelayCommand]
    private void Toggle(StartupEntry? entry)
    {
        if (entry is null) return;
        try
        {
            _startup.SetEnabled(entry, entry.IsEnabled);
            Status = $"{entry.Name} {(entry.IsEnabled ? "enabled" : "disabled")}";
        }
        catch (Exception ex)
        {
            Status = $"Could not change {entry.Name}: {ex.Message}";
            Refresh();
        }
    }

    [RelayCommand]
    private void ApplyGamingPlan()
    {
        Status = _power.ApplyGamingPlan();
        PowerPlan = _power.GetActivePlanName();
    }
}
