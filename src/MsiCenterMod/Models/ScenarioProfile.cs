namespace MsiCenterMod.Models;

/// <summary>
/// Một scenario do người dùng định nghĩa — tương đương một "User Scenario"
/// trong MSI Center nhưng không giới hạn số lượng.
/// </summary>
public sealed class ScenarioProfile
{
    /// <summary>Giá trị mặc định lấy từ Default_Fan của chính máy này (registry MSI Center).</summary>
    public static readonly int[] DefaultCpuFanCurve = [0, 40, 49, 58, 67, 76];
    public static readonly int[] DefaultGpuFanCurve = [0, 48, 56, 64, 72, 79];

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Scenario mới";

    /// <summary>Emoji/ký tự hiển thị trên card scenario.</summary>
    public string Glyph { get; set; } = "🎮";

    public PerformanceLevel Performance { get; set; } = PerformanceLevel.High;

    public FanMode FanMode { get; set; } = FanMode.Auto;

    /// <summary>Tốc độ quạt CPU (%) tại 6 điểm nhiệt — chỉ dùng khi <see cref="FanMode.Advanced"/>.</summary>
    public int[] CpuFanCurve { get; set; } = (int[])DefaultCpuFanCurve.Clone();

    /// <summary>Tốc độ quạt GPU (%) tại 6 điểm nhiệt — chỉ dùng khi <see cref="FanMode.Advanced"/>.</summary>
    public int[] GpuFanCurve { get; set; } = (int[])DefaultGpuFanCurve.Clone();

    public PowerOverlayMode PowerOverlay { get; set; } = PowerOverlayMode.None;

    public ScenarioProfile Clone() => new()
    {
        Id = Guid.NewGuid(),
        Name = Name,
        Glyph = Glyph,
        Performance = Performance,
        FanMode = FanMode,
        CpuFanCurve = (int[])CpuFanCurve.Clone(),
        GpuFanCurve = (int[])GpuFanCurve.Clone(),
        PowerOverlay = PowerOverlay,
    };
}
