namespace MsiCenterMod.Models;

/// <summary>Thông tin một ổ đĩa vật lý cho mục SSD Status.</summary>
public sealed record PhysicalDiskInfo
{
    public required string Model { get; init; }

    public double TotalGb { get; init; }

    public double UsedGb { get; init; }

    public double FreeGb { get; init; }

    public int UsedPercent { get; init; }

    /// <summary>Nhiệt độ (°C) từ MSFT_StorageReliabilityCounter; null nếu không hỗ trợ.</summary>
    public int? TemperatureC { get; init; }

    /// <summary>0 = Healthy, 1 = Warning, 2 = Unhealthy (MSFT_PhysicalDisk.HealthStatus).</summary>
    public int HealthStatus { get; init; }

    public bool IsHealthy => HealthStatus == 0;
}
