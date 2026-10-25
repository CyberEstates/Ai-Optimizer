namespace AIGamingOptimizer.Models;

public sealed class SystemSnapshot
{
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.Now;
    public double CpuPercent { get; init; }
    public double GpuPercent { get; init; }
    public double RamPercent { get; init; }
    public double RamUsedGb { get; init; }
    public double RamTotalGb { get; init; }
    public string CpuName { get; init; } = "CPU";
    public string GpuName { get; init; } = "GPU";
    public string OsName { get; init; } = Environment.OSVersion.ToString();
    public int LogicalProcessors { get; init; } = Environment.ProcessorCount;
    public int ProcessCount { get; init; }
    public int SafeToCloseCount { get; init; }
    public int HealthScore { get; init; }
    public string HealthSummary { get; init; } = string.Empty;
    public bool IsElevated { get; init; }
    public string PowerPlan { get; init; } = "Unknown";
    public bool GameModeEnabled { get; init; }
}
