namespace MsiCenterMod.Models;

/// <summary>
/// Chế độ giới hạn sạc pin (Battery Master của MSI) — EC 0xD7:
/// 7 bit thấp = % dừng sạc, bit 7 giữ nguyên.
/// </summary>
public enum BatteryChargeMode
{
    /// <summary>Chưa xác định (chưa đọc được từ EC).</summary>
    Unknown,

    /// <summary>Best for Mobility — sạc đầy 100%.</summary>
    BestForMobility,

    /// <summary>Balanced — sạc khi dưới 70%, dừng ở 80%.</summary>
    Balanced,

    /// <summary>Best for Battery — sạc khi dưới 50%, dừng ở 60%.</summary>
    BestForBattery,
}

public static class BatteryChargeModeExtensions
{
    /// <summary>% dừng sạc tương ứng (giá trị MSI Center ghi vào EC 0xD7).</summary>
    public static int ToStopPercent(this BatteryChargeMode mode) => mode switch
    {
        BatteryChargeMode.BestForMobility => 100,
        BatteryChargeMode.Balanced => 80,
        BatteryChargeMode.BestForBattery => 60,
        _ => 0,
    };

    public static BatteryChargeMode FromStopPercent(int percent) => percent switch
    {
        >= 95 => BatteryChargeMode.BestForMobility,
        >= 70 => BatteryChargeMode.Balanced,
        >= 50 => BatteryChargeMode.BestForBattery,
        _ => BatteryChargeMode.Unknown,
    };
}
