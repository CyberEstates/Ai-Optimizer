namespace AIGamingOptimizer.Models;

public enum ProcessCategory
{
    Critical,
    SafeToClose,
    UserApp
}

public sealed class ProcessInfoItem
{
    public int Pid { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public double CpuPercent { get; set; }
    public double MemoryMb { get; set; }
    public ProcessCategory Category { get; init; }
    public bool IsSelected { get; set; }
}
