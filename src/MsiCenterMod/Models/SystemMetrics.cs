namespace MsiCenterMod.Models;

/// <summary>Ảnh chụp chỉ số hệ thống cho tab Hardware Monitoring.</summary>
public sealed record SystemMetrics
{
    public double CpuUsagePercent { get; init; }

    /// <summary>% GPU rời (nvidia-smi); null nếu không đọc được.</summary>
    public double? GpuUsagePercent { get; init; }

    public string? GpuName { get; init; }

    public int? GpuCoreClockMhz { get; init; }

    public int? GpuVramClockMhz { get; init; }

    public int? GpuTemperatureC { get; init; }

    public int MemoryUsagePercent { get; init; }

    public double TotalRamGb { get; init; }

    /// <summary>% thời gian ổ đĩa bận (PhysicalDisk % Disk Time, clamp 0–100).</summary>
    public int DiskActivityPercent { get; init; }

    public double SsdAvailableGb { get; init; }

    public int SsdUsedPercent { get; init; }

    /// <summary>Tổng băng thông mạng hiện tại (byte/giây, mọi adapter).</summary>
    public double LanBytesPerSec { get; init; }
}
