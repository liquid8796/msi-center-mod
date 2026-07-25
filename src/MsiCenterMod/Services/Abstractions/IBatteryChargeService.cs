using MsiCenterMod.Models;

namespace MsiCenterMod.Services.Abstractions;

/// <summary>Battery Master: đọc/đặt giới hạn sạc pin qua EC 0xD7 (giao thức MSI Center).</summary>
public interface IBatteryChargeService
{
    /// <summary>% dừng sạc hiện tại (7 bit thấp của EC 0xD7); null nếu không đọc được.</summary>
    Task<int?> ReadChargeStopPercentAsync(CancellationToken ct = default);

    Task<bool> SetModeAsync(BatteryChargeMode mode, CancellationToken ct = default);
}
