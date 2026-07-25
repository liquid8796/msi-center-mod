namespace MsiCenterMod.Models;

/// <summary>Ảnh chụp trạng thái phần cứng tại một thời điểm (đọc từ EC qua WMI).</summary>
public sealed record HardwareStatus
{
    public int CpuTemperature { get; init; }
    public int GpuTemperature { get; init; }
    public int CpuFanRpm { get; init; }
    public int GpuFanRpm { get; init; }

    /// <summary>Giá trị thô của EC 0xD2 (shift mode), ví dụ 0xC4 = Turbo.</summary>
    public byte ShiftModeRaw { get; init; }

    /// <summary>Giá trị thô của EC 0xD4 (fan mode flags).</summary>
    public byte FanModeRaw { get; init; }

    public bool IsCoolerBoostOn { get; init; }

    public PerformanceLevel? Performance => (ShiftModeRaw & 0x07) switch
    {
        4 => PerformanceLevel.Turbo,
        0 => PerformanceLevel.High,
        1 => PerformanceLevel.Balanced,
        2 => PerformanceLevel.Eco,
        _ => null,
    };

    public FanMode CurrentFanMode
    {
        get
        {
            if (IsCoolerBoostOn)
            {
                return FanMode.CoolerBoost;
            }

            if ((FanModeRaw & 0x80) != 0)
            {
                return FanMode.Advanced;
            }

            return (FanModeRaw & 0x10) != 0 ? FanMode.Silent : FanMode.Auto;
        }
    }
}
