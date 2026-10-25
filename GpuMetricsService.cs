using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AIGamingOptimizer.Services;

/// <summary>
/// Reads GPU utilization from NVIDIA NVML when present, otherwise Windows GPU Engine counters.
/// </summary>
public sealed class GpuMetricsService : IDisposable
{
    private bool _nvmlReady;
    private PerformanceCounter[] _gpuCounters = [];
    private DateTime _lastCounterRefresh = DateTime.MinValue;

    public GpuMetricsService()
    {
        TryInitNvml();
    }

    public (double Percent, string Name) Sample()
    {
        if (_nvmlReady)
        {
            var nvml = TrySampleNvml();
            if (nvml is not null)
            {
                return nvml.Value;
            }
        }

        return SamplePdh();
    }

    private void TryInitNvml()
    {
        try
        {
            if (Nvml.nvmlInit_v2() == 0)
            {
                _nvmlReady = true;
            }
        }
        catch (DllNotFoundException)
        {
            _nvmlReady = false;
        }
        catch
        {
            _nvmlReady = false;
        }
    }

    private (double Percent, string Name)? TrySampleNvml()
    {
        try
        {
            if (Nvml.nvmlDeviceGetHandleByIndex_v2(0, out var handle) != 0)
            {
                return null;
            }

            var nameBuf = new byte[96];
            Nvml.nvmlDeviceGetName(handle, nameBuf, (uint)nameBuf.Length);
            var name = System.Text.Encoding.UTF8.GetString(nameBuf).TrimEnd('\0');

            if (Nvml.nvmlDeviceGetUtilizationRates(handle, out var util) != 0)
            {
                return null;
            }

            return (util.gpu, string.IsNullOrWhiteSpace(name) ? "NVIDIA GPU" : name);
        }
        catch
        {
            return null;
        }
    }

    private (double Percent, string Name) SamplePdh()
    {
        try
        {
            if (_gpuCounters.Length == 0 || DateTime.UtcNow - _lastCounterRefresh > TimeSpan.FromSeconds(30))
            {
                RefreshCounters();
            }

            double max = 0;
            foreach (var counter in _gpuCounters)
            {
                try
                {
                    max = Math.Max(max, counter.NextValue());
                }
                catch
                {
                    // Instance can vanish when a GPU engine idles.
                }
            }

            return (Math.Clamp(max, 0, 100), "GPU");
        }
        catch
        {
            return (0, "GPU");
        }
    }

    private void RefreshCounters()
    {
        foreach (var c in _gpuCounters)
        {
            c.Dispose();
        }

        var list = new List<PerformanceCounter>();
        try
        {
            var category = new PerformanceCounterCategory("GPU Engine");
            foreach (var instance in category.GetInstanceNames())
            {
                if (!instance.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                list.Add(new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, true));
            }
        }
        catch
        {
            // GPU Engine category is missing on some systems.
        }

        _gpuCounters = [.. list];
        _lastCounterRefresh = DateTime.UtcNow;
        foreach (var counter in _gpuCounters)
        {
            try { counter.NextValue(); } catch { /* prime */ }
        }
    }

    public void Dispose()
    {
        foreach (var c in _gpuCounters)
        {
            c.Dispose();
        }

        if (_nvmlReady)
        {
            try { Nvml.nvmlShutdown(); } catch { /* ignore */ }
        }
    }

    private static class Nvml
    {
        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int nvmlInit_v2();

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int nvmlShutdown();

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int nvmlDeviceGetName(IntPtr device, byte[] name, uint length);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int nvmlDeviceGetUtilizationRates(IntPtr device, out NvmlUtilization utilization);

        [StructLayout(LayoutKind.Sequential)]
        internal struct NvmlUtilization
        {
            public uint gpu;
            public uint memory;
        }
    }
}
