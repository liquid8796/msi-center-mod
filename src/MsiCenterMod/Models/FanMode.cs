namespace MsiCenterMod.Models;

/// <summary>
/// Chế độ quạt — ánh xạ với thanh ghi EC 0xD4 (bit 7 = Advanced, bit 4 = Silent)
/// và 0x98 bit 7 (Cooler Boost) như MSI Center.
/// </summary>
public enum FanMode
{
    /// <summary>EC tự điều khiển quạt theo đường cong mặc định.</summary>
    Auto,

    /// <summary>Ưu tiên im lặng (EC 0xD4 bit 4).</summary>
    Silent,

    /// <summary>Đường cong quạt tùy chỉnh 6 điểm cho CPU/GPU (EC 0xD4 bit 7).</summary>
    Advanced,

    /// <summary>Quạt chạy tối đa (EC 0x98 bit 7).</summary>
    CoolerBoost,
}
