using System.Collections.ObjectModel;
using AIGamingOptimizer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIGamingOptimizer.ViewModels;

public partial class CleanerViewModel : ObservableObject
{
    private readonly CleanupService _cleanup;

    [ObservableProperty] private string _estimate = "Not scanned yet";
    [ObservableProperty] private string _status = "Scan junk files, then clean.";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<string> Details { get; } = [];

    public CleanerViewModel(CleanupService cleanup)
    {
        _cleanup = cleanup;
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Status = "Measuring temp files and caches…";
        try
        {
            var bytes = await Task.Run(_cleanup.EstimateJunkBytes);
            Estimate = $"{bytes / 1024d / 1024d:0.0} MB of junk found";
            Status = "Ready to clean.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CleanAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Status = "Cleaning…";
        try
        {
            var result = await Task.Run(_cleanup.CleanTempAndCaches);
            Estimate = result.Summary;
            Status = $"{result.FilesSkipped} files were in use and skipped.";
            Details.Clear();
            foreach (var line in result.Details)
            {
                Details.Add(line);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
