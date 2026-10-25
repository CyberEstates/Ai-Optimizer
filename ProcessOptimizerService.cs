using System.Diagnostics;
using AIGamingOptimizer.Models;
using AIGamingOptimizer.Native;

namespace AIGamingOptimizer.Services;

public sealed class ProcessOptimizerService
{
    private readonly Dictionary<int, TimeSpan> _lastCpu = [];
    private DateTime _lastSample = DateTime.UtcNow;

    private static readonly HashSet<string> Critical = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "registry", "smss", "csrss", "wininit", "services", "lsass", "svchost",
        "fontdrvhost", "dwm", "explorer", "winlogon", "sihost", "taskhostw", "runtimebroker",
        "searchhost", "shellexperiencehost", "startmenuexperiencehost", "applicationframehost",
        "securityhealthservice", "msmpeng", "nissrv", "smartscreen", "audiodg", "conhost",
        "dllhost", "wmiprvse", "spoolsv", "lsm", "smss", "idle", "memory compression",
        "system interrupts", "secure system", "aigamingoptimizer"
    };

    private static readonly string[] SafeNameHints =
    [
        "chrome", "msedge", "firefox", "opera", "brave", "discord", "slack", "teams",
        "spotify", "steamwebhelper", "epicwebhelper", "epicgameslauncher", "origin",
        "eadesktop", "battle.net", "agent", "onedrive", "dropbox", "googledrivesync",
        "skype", "zoom", "outlook", "winword", "excel", "powerpnt", "adobe", "ccxprocess",
        "creative cloud", "update", "updater", "crashpad", "software_reporter",
        "yourphone", "phoneexperiencehost", "widgets", "widgetservice", "copilot",
        "ms-teams", "itunes", "applemobiledeviceservice"
    ];

    private static readonly string[] GameNameHints =
    [
        "game", "-win64-shipping", "cs2", "valorant", "league", "fortnite", "overwatch",
        "r5apex", "gta5", "eldenring", "witcher", "cyberpunk", "dota2", "hl2", "hlvr",
        "minecraft", "javaw", "roblox", "wow", "destiny2", "rainbowsix", "cod",
        "modernwarfare", "starfield", "palworld"
    ];

    public IReadOnlyList<ProcessInfoItem> Scan()
    {
        var now = DateTime.UtcNow;
        var wall = (now - _lastSample).TotalSeconds;
        if (wall < 0.2) wall = 0.2;
        _lastSample = now;

        var cores = Math.Max(Environment.ProcessorCount, 1);
        var items = new List<ProcessInfoItem>();
        var seen = new HashSet<int>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                using (process)
                {
                    if (process.HasExited)
                    {
                        continue;
                    }

                    seen.Add(process.Id);
                    var name = string.IsNullOrWhiteSpace(process.ProcessName) ? $"pid-{process.Id}" : process.ProcessName;
                    var title = SafeMainWindowTitle(process);
                    var cpuTime = SafeCpu(process);
                    double cpuPct = 0;
                    if (_lastCpu.TryGetValue(process.Id, out var prev) && cpuTime > prev)
                    {
                        cpuPct = (cpuTime - prev).TotalSeconds / (wall * cores) * 100.0;
                    }

                    _lastCpu[process.Id] = cpuTime;
                    var memMb = process.WorkingSet64 / 1024d / 1024d;
                    var category = Categorize(name);

                    items.Add(new ProcessInfoItem
                    {
                        Pid = process.Id,
                        Name = name,
                        Title = title,
                        CpuPercent = Math.Clamp(cpuPct, 0, 100),
                        MemoryMb = memMb,
                        Category = category,
                        IsSelected = category == ProcessCategory.SafeToClose
                    });
                }
            }
            catch
            {
                // Access denied / exited mid-loop.
            }
        }

        foreach (var stale in _lastCpu.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _lastCpu.Remove(stale);
        }

        return items
            .OrderByDescending(p => p.CpuPercent)
            .ThenByDescending(p => p.MemoryMb)
            .ToList();
    }

    public BoostResult GameBoost(IEnumerable<int>? pidsToClose = null)
    {
        var scan = Scan();
        var targets = pidsToClose is null
            ? scan.Where(p => p.Category == ProcessCategory.SafeToClose).Select(p => p.Pid).ToHashSet()
            : pidsToClose.ToHashSet();

        var closed = new List<string>();
        var skipped = new List<string>();

        foreach (var item in scan.Where(p => targets.Contains(p.Pid)))
        {
            if (item.Category == ProcessCategory.Critical || item.Pid == Environment.ProcessId)
            {
                skipped.Add($"{item.Name} (protected)");
                continue;
            }

            try
            {
                using var process = Process.GetProcessById(item.Pid);
                process.Kill(entireProcessTree: true);
                closed.Add($"{item.Name} (PID {item.Pid})");
            }
            catch (Exception ex)
            {
                skipped.Add($"{item.Name}: {ex.Message}");
            }
        }

        var boosted = BoostForegroundOrGame();
        TrimWorkingSets();

        return new BoostResult(closed, skipped, boosted);
    }

    public int CloseMatching(string hint)
    {
        var closed = 0;
        foreach (var item in Scan())
        {
            if (item.Category == ProcessCategory.Critical)
            {
                continue;
            }

            if (!item.Name.Contains(hint, StringComparison.OrdinalIgnoreCase) &&
                !item.Title.Contains(hint, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                using var process = Process.GetProcessById(item.Pid);
                process.Kill(entireProcessTree: true);
                closed++;
            }
            catch
            {
                // ignore
            }
        }

        return closed;
    }

    public int CloseBrowsers()
    {
        string[] browsers = ["chrome", "msedge", "firefox", "opera", "brave", "iexplore"];
        var closed = 0;
        foreach (var browser in browsers)
        {
            closed += CloseMatching(browser);
        }

        return closed;
    }

    private string? BoostForegroundOrGame()
    {
        try
        {
            using var search = new ProcessSearch();
            var game = search.FirstGame();
            var target = game ?? search.HeaviestWindow();
            if (target is null)
            {
                return null;
            }

            target.PriorityClass = ProcessPriorityClass.High;
            return $"{target.ProcessName} set to High priority";
        }
        catch
        {
            return null;
        }
    }

    private static void TrimWorkingSets()
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == Environment.ProcessId)
                {
                    continue;
                }

                NativeMethods.EmptyWorkingSet(process.Handle);
            }
            catch
            {
                // Many system processes deny EmptyWorkingSet.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static ProcessCategory Categorize(string name)
    {
        if (Critical.Contains(name))
        {
            return ProcessCategory.Critical;
        }

        if (SafeNameHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase)))
        {
            return ProcessCategory.SafeToClose;
        }

        return ProcessCategory.UserApp;
    }

    private static bool LooksLikeGame(string name, string title)
    {
        return GameNameHints.Any(h =>
            name.Contains(h, StringComparison.OrdinalIgnoreCase) ||
            title.Contains(h, StringComparison.OrdinalIgnoreCase));
    }

    private static TimeSpan SafeCpu(Process process)
    {
        try { return process.TotalProcessorTime; }
        catch { return TimeSpan.Zero; }
    }

    private static string SafeMainWindowTitle(Process process)
    {
        try { return process.MainWindowTitle ?? string.Empty; }
        catch { return string.Empty; }
    }
}

public sealed record BoostResult(IReadOnlyList<string> Closed, IReadOnlyList<string> Skipped, string? BoostedApp);

file sealed class ProcessSearch : IDisposable
{
    private readonly List<Process> _owned = [.. Process.GetProcesses()];

    public Process? FirstGame() =>
        _owned.FirstOrDefault(p =>
        {
            try
            {
                return LooksLikeGame(p.ProcessName, SafeTitle(p));
            }
            catch
            {
                return false;
            }
        });

    public Process? HeaviestWindow() =>
        _owned
            .Where(p =>
            {
                try { return !string.IsNullOrWhiteSpace(p.MainWindowTitle); }
                catch { return false; }
            })
            .OrderByDescending(p =>
            {
                try { return p.WorkingSet64; }
                catch { return 0L; }
            })
            .FirstOrDefault();

    private static bool LooksLikeGame(string name, string title)
    {
        string[] hints =
        [
            "game", "-win64-shipping", "cs2", "valorant", "league", "fortnite", "overwatch",
            "r5apex", "gta5", "eldenring", "witcher", "cyberpunk", "dota2", "minecraft",
            "javaw", "roblox", "wow", "destiny2", "rainbowsix", "starfield", "palworld"
        ];
        return hints.Any(h =>
            name.Contains(h, StringComparison.OrdinalIgnoreCase) ||
            title.Contains(h, StringComparison.OrdinalIgnoreCase));
    }

    private static string SafeTitle(Process process)
    {
        try { return process.MainWindowTitle ?? string.Empty; }
        catch { return string.Empty; }
    }

    public void Dispose()
    {
        foreach (var process in _owned)
        {
            process.Dispose();
        }
    }
}
