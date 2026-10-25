using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AIGamingOptimizer.Services;

public sealed class PowerPlanService
{
    public static readonly Guid HighPerformance = Guid.Parse("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    public static readonly Guid UltimatePerformance = Guid.Parse("e9a42b02-d5df-448d-aa00-4b01e5d6fd52");

    public string GetActivePlanName()
    {
        var output = RunPowerCfg("/getactivescheme");
        var match = Regex.Match(output, @"\((.+)\)");
        return match.Success ? match.Groups[1].Value : "Unknown";
    }

    public string ApplyGamingPlan()
    {
        RunPowerCfg($"/duplicatescheme {UltimatePerformance}");
        var ultimate = RunPowerCfg($"/setactive {UltimatePerformance}");
        if (!ultimate.Contains("error", StringComparison.OrdinalIgnoreCase) &&
            !ultimate.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            HardwareInfoService.SetGameMode(true);
            return "Ultimate Performance power plan + Game Mode";
        }

        RunPowerCfg($"/setactive {HighPerformance}");
        HardwareInfoService.SetGameMode(true);
        return "High Performance power plan + Game Mode";
    }

    private static string RunPowerCfg(string args)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return string.Empty;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(4000);
            return stdout + stderr;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
