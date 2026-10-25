using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AIGamingOptimizer.Services;

public sealed class HardwareInfoService
{
    private readonly Lazy<string> _cpuName = new(ReadCpuName);
    private readonly Lazy<string> _gpuName = new(ReadGpuName);
    private readonly Lazy<string> _osName = new(ReadOsName);

    public string CpuName => _cpuName.Value;
    public string GpuName => _gpuName.Value;
    public string OsName => _osName.Value;

    public static bool IsElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    public static bool IsGameModeEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
            var raw = key?.GetValue("AutoGameModeEnabled") ?? key?.GetValue("AllowAutoGameMode");
            return raw is int flag && flag == 1;
        }
        catch
        {
            return false;
        }
    }

    public static void SetGameMode(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar");
        key.SetValue("AutoGameModeEnabled", enabled ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("AllowAutoGameMode", enabled ? 1 : 0, RegistryValueKind.DWord);
    }

    private static string ReadCpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            if (key?.GetValue("ProcessorNameString") is string name && !string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }
        }
        catch
        {
            // fall through
        }

        return RuntimeInformation.ProcessArchitecture + " CPU";
    }

    private static string ReadGpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name) &&
                    !name.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }
        }
        catch
        {
            // WMI can be restricted.
        }

        return "GPU";
    }

    private static string ReadOsName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var product = key?.GetValue("ProductName") as string ?? "Windows";
            var build = key?.GetValue("CurrentBuildNumber") as string ?? "";
            var display = key?.GetValue("DisplayVersion") as string ?? "";
            return string.Join(" ", new[] { product, display, string.IsNullOrEmpty(build) ? null : $"({build})" }.Where(s => s is not null));
        }
        catch
        {
            return Environment.OSVersion.VersionString;
        }
    }
}
