using System.Text.Json;
using AIGamingOptimizer.Models;

namespace AIGamingOptimizer.Services;

/// <summary>
/// Live tool-use surface the AI assistant can call against this PC.
/// </summary>
public sealed class SystemToolService
{
    private readonly SystemMetricsService _metrics;
    private readonly ProcessOptimizerService _processes;
    private readonly CleanupService _cleanup;
    private readonly PowerPlanService _power;
    private readonly StartupService _startup;

    public SystemToolService(
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
    }

    public string Execute(string name, JsonElement args)
    {
        return name switch
        {
            "get_system_snapshot" => SnapshotJson(),
            "get_top_processes" => TopProcessesJson(ReadString(args, "metric") ?? "cpu"),
            "game_boost" => GameBoostJson(),
            "close_browsers" => Json(new { closed = _processes.CloseBrowsers() }),
            "close_processes" => CloseByHint(ReadString(args, "hint") ?? "browser"),
            "clean_temp_files" => Json(_cleanup.CleanTempAndCaches()),
            "apply_gaming_power_plan" => Json(new { applied = _power.ApplyGamingPlan() }),
            "analyze_system" => AnalyzeJson(),
            _ => Json(new { error = $"Unknown tool '{name}'" })
        };
    }

    public string SnapshotJson()
    {
        var snap = _metrics.Capture();
        return Json(new
        {
            snap.CpuPercent,
            snap.GpuPercent,
            snap.RamPercent,
            snap.RamUsedGb,
            snap.RamTotalGb,
            snap.CpuName,
            snap.GpuName,
            snap.OsName,
            snap.ProcessCount,
            snap.SafeToCloseCount,
            snap.HealthScore,
            snap.HealthSummary,
            snap.PowerPlan,
            snap.GameModeEnabled,
            snap.IsElevated
        });
    }

    public string TopProcessesJson(string metric)
    {
        var items = _processes.Scan();
        IEnumerable<ProcessInfoItem> ordered = metric.ToLowerInvariant() switch
        {
            "ram" or "memory" => items.OrderByDescending(p => p.MemoryMb),
            "gpu" => items.OrderByDescending(p => p.CpuPercent),
            _ => items.OrderByDescending(p => p.CpuPercent)
        };

        return Json(ordered.Take(12).Select(p => new
        {
            p.Pid,
            p.Name,
            p.Title,
            cpu = Math.Round(p.CpuPercent, 1),
            ramMb = Math.Round(p.MemoryMb, 1),
            category = p.Category.ToString()
        }));
    }

    private string GameBoostJson()
    {
        var result = _processes.GameBoost();
        var power = _power.ApplyGamingPlan();
        return Json(new
        {
            closed = result.Closed,
            skipped = result.Skipped,
            boosted = result.BoostedApp,
            power
        });
    }

    private string CloseByHint(string hint)
    {
        if (hint.Contains("browser", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new { closed = _processes.CloseBrowsers() });
        }

        return Json(new { closed = _processes.CloseMatching(hint) });
    }

    private string AnalyzeJson()
    {
        var snap = _metrics.Capture();
        var junk = _cleanup.EstimateJunkBytes();
        var startup = _startup.ListEntries();
        return Json(new
        {
            snap.HealthScore,
            snap.HealthSummary,
            snap.CpuPercent,
            snap.GpuPercent,
            snap.RamPercent,
            snap.SafeToCloseCount,
            junkMb = Math.Round(junk / 1024d / 1024d, 1),
            startupEnabled = startup.Count(s => s.IsEnabled),
            startupTotal = startup.Count,
            snap.PowerPlan,
            snap.GameModeEnabled
        });
    }

    private static string? ReadString(JsonElement args, string name)
    {
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value))
        {
            return value.GetString();
        }

        return null;
    }

    private static string Json(object value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}
