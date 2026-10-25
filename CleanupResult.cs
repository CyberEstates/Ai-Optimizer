namespace AIGamingOptimizer.Models;

public sealed class CleanupResult
{
    public long BytesFreed { get; init; }
    public int FilesRemoved { get; init; }
    public int FilesSkipped { get; init; }
    public IReadOnlyList<string> Details { get; init; } = [];
    public string Summary => $"{FilesRemoved} files removed · {BytesFreed / 1024d / 1024d:0.0} MB freed";
}
