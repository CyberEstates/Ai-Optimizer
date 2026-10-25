using System.IO;
using AIGamingOptimizer.Models;
using Microsoft.Win32;

namespace AIGamingOptimizer.Services;

public sealed class CleanupService
{
    public CleanupResult CleanTempAndCaches()
    {
        var details = new List<string>();
        long bytes = 0;
        int removed = 0;
        int skipped = 0;

        var targets = EnumerateCleanupRoots().Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var root in targets)
        {
            var result = CleanDirectory(root);
            bytes += result.Bytes;
            removed += result.Removed;
            skipped += result.Skipped;
            if (result.Removed > 0)
            {
                details.Add($"{root}: {result.Removed} files / {result.Bytes / 1024d / 1024d:0.0} MB");
            }
        }

        var traces = ClearRecentTraces();
        details.AddRange(traces);

        return new CleanupResult
        {
            BytesFreed = bytes,
            FilesRemoved = removed,
            FilesSkipped = skipped,
            Details = details
        };
    }

    public long EstimateJunkBytes()
    {
        long total = 0;
        foreach (var root in EnumerateCleanupRoots())
        {
            total += MeasureDirectory(root);
        }

        return total;
    }

    private static IEnumerable<string> EnumerateCleanupRoots()
    {
        yield return Path.GetTempPath();
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, @"Google\Chrome\User Data\Default\Cache");
        yield return Path.Combine(local, @"Google\Chrome\User Data\Default\Code Cache");
        yield return Path.Combine(local, @"Microsoft\Edge\User Data\Default\Cache");
        yield return Path.Combine(local, @"Microsoft\Edge\User Data\Default\Code Cache");
        yield return Path.Combine(local, @"Mozilla\Firefox\Profiles");
        yield return Path.Combine(local, @"NVIDIA\GLCache");
        yield return Path.Combine(local, @"D3DSCache");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SoftwareDistribution\Download");
    }

    private static (long Bytes, int Removed, int Skipped) CleanDirectory(string root)
    {
        if (!Directory.Exists(root))
        {
            return (0, 0, 0);
        }

        long bytes = 0;
        int removed = 0;
        int skipped = 0;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);
        }
        catch
        {
            return (0, 0, 0);
        }

        foreach (var file in files)
        {
            try
            {
                var info = new FileInfo(file);
                var size = info.Length;
                info.Attributes = FileAttributes.Normal;
                info.Delete();
                bytes += size;
                removed++;
            }
            catch
            {
                skipped++;
            }
        }

        return (bytes, removed, skipped);
    }

    private static long MeasureDirectory(string root)
    {
        if (!Directory.Exists(root))
        {
            return 0;
        }

        try
        {
            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(f =>
                {
                    try { return new FileInfo(f).Length; }
                    catch { return 0L; }
                })
                .Sum();
        }
        catch
        {
            return 0;
        }
    }

    private static IEnumerable<string> ClearRecentTraces()
    {
        var notes = new List<string>();
        notes.AddRange(ClearRunMru());
        notes.AddRange(ClearTypedPaths());
        return notes;
    }

    private static IEnumerable<string> ClearRunMru()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU", writable: true);
        if (key is null)
        {
            return [];
        }

        foreach (var name in key.GetValueNames())
        {
            try { key.DeleteValue(name, throwOnMissingValue: false); }
            catch { /* ignore */ }
        }

        return ["Cleared Run dialog history traces."];
    }

    private static IEnumerable<string> ClearTypedPaths()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths", writable: true);
        if (key is null)
        {
            return [];
        }

        foreach (var name in key.GetValueNames())
        {
            try { key.DeleteValue(name, throwOnMissingValue: false); }
            catch { /* ignore */ }
        }

        return ["Cleared Explorer typed-path traces."];
    }
}
