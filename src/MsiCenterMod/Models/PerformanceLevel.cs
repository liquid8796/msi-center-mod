namespace MsiCenterMod.Models;

/// <summary>
/// Mức hiệu năng (shift mode) — ánh xạ 1:1 với giá trị EC 0xD2 mà MSI Center ghi.
/// </summary>
public enum PerformanceLevel
{
    /// <summary>Hiệu năng tối đa (EC 0xC4 — MSI "Turbo/Sport").</summary>
    Turbo,

    /// <summary>Hiệu năng cao (EC 0xC0 — MSI "Comfort").</summary>
    High,

    /// <summary>Cân bằng (EC 0xC1 — MSI "Green").</summary>
    Balanced,

    /// <summary>Tiết kiệm pin (EC 0xC2 — MSI "ECO").</summary>
    Eco,
}
