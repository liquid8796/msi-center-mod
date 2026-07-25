using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using MsiCenterMod.Models;
using MsiCenterMod.Services.Abstractions;

namespace MsiCenterMod.Services.System;

/// <summary>
/// Đọc chỉ số hệ thống bằng API chuẩn của Windows:
///  - CPU/đĩa/mạng: PerformanceCounter (giá trị giống Task Manager).
///  - RAM: GlobalMemoryStatusEx.
///  - GPU rời: nvidia-smi (usage/clock/temp) — chỉ được gọi khi tab Monitoring
///    đang mở, tránh giữ dGPU thức gây tốn pin trên laptop Optimus.
/// Mọi nguồn đọc đều độc lập: nguồn nào lỗi thì trả giá trị mặc định, không phá snapshot.
/// </summary>
public sealed class SystemMetricsService : ISystemMetricsService, IDisposable
{
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _diskCounter;
    private readonly List<PerformanceCounter> _networkCounters = [];
    private readonly string? _nvidiaSmiPath;
    private readonly string? _fallbackGpuName;
    private readonly double _totalRamGb;

    public string CpuName { get; }

    public SystemMetricsService()
    {
        CpuName = ReadCpuNameFromRegistry();
        _totalRamGb = ReadTotalRamGb();
        _nvidiaSmiPath = ProbeNvidiaSmi();
        _fallbackGpuName = _nvidiaSmiPath is null ? ReadGpuNameFromWmi() : null;

        // "% Processor Utility" là số Task Manager hiển thị (Win8+); fallback "% Processor Time".
        _cpuCounter = TryCreateCounter("Processor Information", "% Processor Utility", "_Total")
                      ?? TryCreateCounter("Processor", "% Processor Time", "_Total");
        _diskCounter = TryCreateCounter("PhysicalDisk", "% Disk Time", "_Total");

        try
        {
            foreach (string instance in new PerformanceCounterCategory("Network Interface").GetInstanceNames())
            {
                PerformanceCounter? counter = TryCreateCounter("Network Interface", "Bytes Total/sec", instance);
                if (counter is not null)
                {
                    _networkCounters.Add(counter);
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Không liệt kê được network counter", ex);
        }
    }

    public Task<SystemMetrics> ReadAsync(bool includeGpu = true, CancellationToken ct = default)
        => Task.Run(() => Read(includeGpu), ct);

    private SystemMetrics Read(bool includeGpu)
    {
        (int memoryPercent, _) = ReadMemoryStatus();
        (double ssdAvailableGb, int ssdUsedPercent) = ReadSystemDrive();
        NvidiaGpuInfo? gpu = includeGpu ? ReadNvidiaSmi() : null;

        return new SystemMetrics
        {
            CpuUsagePercent = SampleCounter(_cpuCounter),
            DiskActivityPercent = (int)Math.Round(SampleCounter(_diskCounter)),
            MemoryUsagePercent = memoryPercent,
            TotalRamGb = _totalRamGb,
            SsdAvailableGb = ssdAvailableGb,
            SsdUsedPercent = ssdUsedPercent,
            LanBytesPerSec = _networkCounters.Sum(c => SampleCounterRaw(c)),
            GpuName = gpu?.Name ?? _fallbackGpuName,
            GpuUsagePercent = gpu?.UsagePercent,
            GpuCoreClockMhz = gpu?.CoreClockMhz,
            GpuVramClockMhz = gpu?.VramClockMhz,
            GpuTemperatureC = gpu?.TemperatureC,
        };
    }

    // ---------- Performance counters ----------

    private static PerformanceCounter? TryCreateCounter(string category, string counter, string instance)
    {
        try
        {
            var pc = new PerformanceCounter(category, counter, instance, readOnly: true);
            _ = pc.NextValue(); // mẫu đầu tiên luôn là 0 — "làm nóng" ngay
            return pc;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Không tạo được counter {category}\\{counter}\\{instance}", ex);
            return null;
        }
    }

    private static double SampleCounter(PerformanceCounter? counter)
        => Math.Clamp(SampleCounterRaw(counter), 0, 100);

    private static double SampleCounterRaw(PerformanceCounter? counter)
    {
        try
        {
            return counter?.NextValue() ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    // ---------- RAM ----------

    private static (int Percent, double TotalGb) ReadMemoryStatus()
    {
        var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            return (0, 0);
        }

        return ((int)status.dwMemoryLoad, status.ullTotalPhys / 1024.0 / 1024 / 1024);
    }

    private static double ReadTotalRamGb() => Math.Round(ReadMemoryStatus().TotalGb);

    // ---------- Ổ đĩa hệ thống ----------

    private static (double AvailableGb, int UsedPercent) ReadSystemDrive()
    {
        try
        {
            string systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(systemRoot);
            double totalGb = drive.TotalSize / 1024.0 / 1024 / 1024;
            double freeGb = drive.AvailableFreeSpace / 1024.0 / 1024 / 1024;
            int usedPercent = totalGb <= 0 ? 0 : (int)Math.Round((totalGb - freeGb) / totalGb * 100);
            return (freeGb, usedPercent);
        }
        catch
        {
            return (0, 0);
        }
    }

    // ---------- GPU (nvidia-smi) ----------

    private sealed record NvidiaGpuInfo(string Name, double UsagePercent, int CoreClockMhz, int VramClockMhz, int TemperatureC);

    private static string? ProbeNvidiaSmi()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe"),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private NvidiaGpuInfo? ReadNvidiaSmi()
    {
        if (_nvidiaSmiPath is null)
        {
            return null;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = _nvidiaSmiPath,
                Arguments = "--query-gpu=name,utilization.gpu,clocks.current.graphics,clocks.current.memory,temperature.gpu --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                return null;
            }

            string output = process.StandardOutput.ReadLine() ?? string.Empty;
            if (!process.WaitForExit(2000))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            string[] parts = output.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 5)
            {
                return null;
            }

            return new NvidiaGpuInfo(
                parts[0],
                ParseOrZero(parts[1]),
                (int)ParseOrZero(parts[2]),
                (int)ParseOrZero(parts[3]),
                (int)ParseOrZero(parts[4]));
        }
        catch (Exception ex)
        {
            AppLogger.Error("nvidia-smi thất bại", ex);
            return null;
        }

        static double ParseOrZero(string s)
            => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0;
    }

    // ---------- Tên phần cứng ----------

    private static string ReadCpuNameFromRegistry()
    {
        try
        {
            return Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "ProcessorNameString", null) as string ?? "CPU không xác định";
        }
        catch
        {
            return "CPU không xác định";
        }
    }

    private static string? ReadGpuNameFromWmi()
    {
        try
        {
            using var searcher = new global::System.Management.ManagementObjectSearcher(
                "SELECT Name FROM Win32_VideoController");
            string? best = null;
            foreach (global::System.Management.ManagementBaseObject item in searcher.Get())
            {
                string? name = item["Name"] as string;
                if (name is null)
                {
                    continue;
                }

                best ??= name;
                if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                {
                    return name; // ưu tiên GPU rời
                }
            }

            return best;
        }
        catch
        {
            return null;
        }
    }

    // ---------- P/Invoke ----------

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    public void Dispose()
    {
        _cpuCounter?.Dispose();
        _diskCounter?.Dispose();
        foreach (PerformanceCounter counter in _networkCounters)
        {
            counter.Dispose();
        }

        _networkCounters.Clear();
    }
}
