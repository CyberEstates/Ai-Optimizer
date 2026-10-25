using System.IO;
using AIGamingOptimizer.Models;
using Microsoft.Win32;

namespace AIGamingOptimizer.Services;

public sealed class StartupService
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string DisabledPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public IReadOnlyList<StartupEntry> ListEntries()
    {
        var items = new List<StartupEntry>();
        items.AddRange(ReadHive(Registry.CurrentUser, "HKCU"));
        items.AddRange(ReadHive(Registry.LocalMachine, "HKLM"));
        items.AddRange(ReadStartupFolder());
        return items.OrderBy(e => e.Name).ToList();
    }

    public void SetEnabled(StartupEntry entry, bool enabled)
    {
        if (entry.Hive is "Folder")
        {
            ToggleStartupFile(entry, enabled);
            return;
        }

        var hive = entry.Hive == "HKLM" ? Registry.LocalMachine : Registry.CurrentUser;
        using var approved = hive.CreateSubKey(DisabledPath);
        var payload = new byte[12];
        payload[0] = enabled ? (byte)0x02 : (byte)0x03;
        approved.SetValue(entry.Name, payload, RegistryValueKind.Binary);
        entry.IsEnabled = enabled;
    }

    private static IEnumerable<StartupEntry> ReadHive(RegistryKey hive, string hiveName)
    {
        using var run = hive.OpenSubKey(RunPath);
        if (run is null)
        {
            yield break;
        }

        Dictionary<string, bool> approved = [];
        using (var approvedKey = hive.OpenSubKey(DisabledPath))
        {
            if (approvedKey is not null)
            {
                foreach (var name in approvedKey.GetValueNames())
                {
                    if (approvedKey.GetValue(name) is byte[] data && data.Length > 0)
                    {
                        approved[name] = data[0] is 0x02 or 0x06;
                    }
                }
            }
        }

        foreach (var name in run.GetValueNames())
        {
            var command = run.GetValue(name)?.ToString() ?? string.Empty;
            approved.TryGetValue(name, out var enabled);
            if (!approved.ContainsKey(name))
            {
                enabled = true;
            }

            yield return new StartupEntry
            {
                Name = name,
                Command = command,
                Location = $"{hiveName}\\...\\Run",
                Hive = hiveName,
                IsEnabled = enabled
            };
        }
    }

    private static IEnumerable<StartupEntry> ReadStartupFolder()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (!Directory.Exists(folder))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var disabled = file.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
            yield return new StartupEntry
            {
                Name = disabled ? Path.GetFileNameWithoutExtension(name) : name,
                Command = file,
                Location = "Startup folder",
                Hive = "Folder",
                IsEnabled = !disabled
            };
        }
    }

    private static void ToggleStartupFile(StartupEntry entry, bool enabled)
    {
        var path = entry.Command;
        if (enabled && path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
        {
            var restored = path[..^".disabled".Length];
            File.Move(path, restored, overwrite: true);
        }
        else if (!enabled && File.Exists(path) && !path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
        {
            File.Move(path, path + ".disabled", overwrite: true);
        }

        entry.IsEnabled = enabled;
    }
}
