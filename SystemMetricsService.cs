using System.Diagnostics;
using AIGamingOptimizer.Models;
using AIGamingOptimizer.Native;

namespace AIGamingOptimizer.Services;

public sealed class SystemMetricsService : IDisposable
{
    private readonly GpuMetricsService _gpu;
    private readonly HardwareInfoService _hardware;
    private readonly PowerPlanService _power;
    private readonly ProcessOptimizerService _processes;
    private ulong _prevIdle;
    private ulong _prevKernel;
    private ulong _prevUser;
    private bool _cpuPrimed;

    public SystemMetricsService(
        GpuMetricsService gpu,
        HardwareInfoService hardware,
        PowerPlanService power,
        ProcessOptimizerService processes)
    {
        _gpu = gpu;
        _hardware = hardware;
        _power = power;
        _processes = processes;
        NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user);
        _prevIdle = idle.ToUInt64();
        _prevKernel = kernel.ToUInt64();
        _prevUser = user.ToUInt64();
    }

    public SystemSnapshot Capture()
    {
        var cpu = SampleCpu();
        var (gpu, gpuName) = _gpu.Sample();
        var mem = SampleMemory();
        var scan = _processes.Scan();
        var bloat = scan.Count(p => p.Category == ProcessCategory.SafeToClose);
        var health = ComputeHealth(cpu, gpu, mem.Percent, bloat);

        return new SystemSnapshot
        {
            CpuPercent = cpu,
            GpuPercent = gpu,
            RamPercent = mem.Percent,
            RamUsedGb = mem.UsedGb,
            RamTotalGb = mem.TotalGb,
            CpuName = _hardware.CpuName,
            GpuName = string.Equals(gpuName, "GPU", StringComparison.Ordinal) ? _hardware.GpuName : gpuName,
            OsName = _hardware.OsName,
            LogicalProcessors = Environment.ProcessorCount,
            ProcessCount = scan.Count,
            SafeToCloseCount = bloat,
            HealthScore = health.Score,
            HealthSummary = health.Summary,
            IsElevated = HardwareInfoService.IsElevated(),
            PowerPlan = _power.GetActivePlanName(),
            GameModeEnabled = HardwareInfoService.IsGameModeEnabled()
        };
    }

    private double SampleCpu()
    {
        if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return 0;
        }

        var idleT = idle.ToUInt64();
        var kernelT = kernel.ToUInt64();
        var userT = user.ToUInt64();

        if (!_cpuPrimed)
        {
            _cpuPrimed = true;
            _prevIdle = idleT;
            _prevKernel = kernelT;
            _prevUser = userT;
            return 0;
        }

        var idleDelta = idleT - _prevIdle;
        var totalDelta = (kernelT - _prevKernel) + (userT - _prevUser);
        _prevIdle = idleT;
        _prevKernel = kernelT;
        _prevUser = userT;

        if (totalDelta == 0)
        {
            return 0;
        }

        var busy = 1.0 - (idleDelta / (double)totalDelta);
        return Math.Clamp(busy * 100.0, 0, 100);
    }

    private static (double Percent, double UsedGb, double TotalGb) SampleMemory()
    {
        var status = new NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>()
        };

        if (!NativeMethods.GlobalMemoryStatusEx(ref status) || status.ullTotalPhys == 0)
        {
            var info = GC.GetGCMemoryInfo();
            var total = info.TotalAvailableMemoryBytes;
            var used = total > 0 ? total - info.TotalCommittedBytes : 0;
            return (0, used / 1024d / 1024d / 1024d, total / 1024d / 1024d / 1024d);
        }

        var usedBytes = status.ullTotalPhys - status.ullAvailPhys;
        return (
            status.dwMemoryLoad,
            usedBytes / 1024d / 1024d / 1024d,
            status.ullTotalPhys / 1024d / 1024d / 1024d);
    }

    private static (int Score, string Summary) ComputeHealth(double cpu, double gpu, double ram, int bloat)
    {
        var penalty = cpu * 0.28 + ram * 0.32 + gpu * 0.18 + Math.Min(bloat, 25) * 1.2;
        var score = (int)Math.Clamp(100 - penalty, 12, 99);
        var summary = score switch
        {
            >= 85 => "System looks healthy for gaming. Background load is light.",
            >= 70 => "We've detected moderate background activity that can steal frames.",
            _ => "We've detected heavy background processes impacting gaming performance."
        };
        return (score, summary);
    }

    public void Dispose() => _gpu.Dispose();
}
