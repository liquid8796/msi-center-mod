using System.Management;
using MsiCenterMod.Models;
using MsiCenterMod.Services.Abstractions;

namespace MsiCenterMod.Services.System;

/// <summary>
/// Thông tin ổ đĩa vật lý:
///  - Model/dung lượng + map phân vùng: Win32_DiskDrive → DiskPartition → LogicalDisk.
///  - Nhiệt độ + sức khỏe: root\Microsoft\Windows\Storage
///    (MSFT_PhysicalDisk, MSFT_StorageReliabilityCounter — cần quyền admin).
/// </summary>
public sealed class StorageInfoService : IStorageInfoService
{
    private const double BytesPerGb = 1024.0 * 1024 * 1024;

    public Task<IReadOnlyList<PhysicalDiskInfo>> ReadDisksAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<PhysicalDiskInfo>>(Read, ct);

    private static List<PhysicalDiskInfo> Read()
    {
        Dictionary<uint, (int Health, int? Temp)> reliability = ReadReliability();
        var disks = new List<PhysicalDiskInfo>();

        try
        {
            using var driveSearcher = new ManagementObjectSearcher(
                "SELECT DeviceID, Model, Size, Index FROM Win32_DiskDrive");
            foreach (ManagementBaseObject drive in driveSearcher.Get())
            {
                uint index = (uint)drive["Index"];
                string model = drive["Model"] as string ?? "Ổ đĩa không xác định";
                double totalGb = Convert.ToUInt64(drive["Size"] ?? 0UL) / BytesPerGb;

                (double usedGb, double freeGb) = SumVolumes(drive["DeviceID"] as string ?? string.Empty);
                double usableGb = usedGb + freeGb;
                int usedPercent = usableGb > 0 ? (int)Math.Round(usedGb / usableGb * 100) : 0;

                (int health, int? temp) = reliability.TryGetValue(index, out var info) ? info : (0, null);

                disks.Add(new PhysicalDiskInfo
                {
                    Model = model,
                    TotalGb = totalGb,
                    UsedGb = usedGb,
                    FreeGb = freeGb,
                    UsedPercent = usedPercent,
                    TemperatureC = temp,
                    HealthStatus = health,
                });
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Liệt kê Win32_DiskDrive thất bại", ex);
        }

        return disks;
    }

    /// <summary>Cộng dồn dung lượng các volume có ký tự ổ đĩa thuộc một ổ vật lý.</summary>
    private static (double UsedGb, double FreeGb) SumVolumes(string deviceId)
    {
        double totalBytes = 0, freeBytes = 0;
        try
        {
            using var partitionSearcher = new ManagementObjectSearcher(
                $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{deviceId}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition");
            foreach (ManagementBaseObject partition in partitionSearcher.Get())
            {
                using var logicalSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass = Win32_LogicalDiskToPartition");
                foreach (ManagementBaseObject logical in logicalSearcher.Get())
                {
                    totalBytes += Convert.ToUInt64(logical["Size"] ?? 0UL);
                    freeBytes += Convert.ToUInt64(logical["FreeSpace"] ?? 0UL);
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Map phân vùng cho {deviceId} thất bại", ex);
        }

        return ((totalBytes - freeBytes) / BytesPerGb, freeBytes / BytesPerGb);
    }

    /// <summary>Đọc HealthStatus + Temperature theo chỉ số ổ đĩa từ Storage namespace.</summary>
    private static Dictionary<uint, (int Health, int? Temp)> ReadReliability()
    {
        var result = new Dictionary<uint, (int, int?)>();
        try
        {
            var scope = new ManagementScope(@"root\Microsoft\Windows\Storage");
            scope.Connect();

            var health = new Dictionary<uint, int>();
            using (var searcher = new ManagementObjectSearcher(
                scope, new ObjectQuery("SELECT DeviceId, HealthStatus FROM MSFT_PhysicalDisk")))
            {
                foreach (ManagementBaseObject disk in searcher.Get())
                {
                    if (uint.TryParse(disk["DeviceId"] as string, out uint id))
                    {
                        health[id] = Convert.ToInt32(disk["HealthStatus"] ?? 0);
                    }
                }
            }

            using (var searcher = new ManagementObjectSearcher(
                scope, new ObjectQuery("SELECT DeviceId, Temperature FROM MSFT_StorageReliabilityCounter")))
            {
                foreach (ManagementBaseObject counter in searcher.Get())
                {
                    if (!uint.TryParse(counter["DeviceId"] as string, out uint id))
                    {
                        continue;
                    }

                    int temp = Convert.ToInt32(counter["Temperature"] ?? 0);
                    result[id] = (health.GetValueOrDefault(id), temp > 0 ? temp : null);
                }
            }

            // Ổ có health nhưng không có reliability counter
            foreach ((uint id, int healthStatus) in health)
            {
                result.TryAdd(id, (healthStatus, null));
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Đọc Storage namespace thất bại (nhiệt độ/sức khỏe SSD)", ex);
        }

        return result;
    }
}
