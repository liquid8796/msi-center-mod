using MsiCenterMod.Models;
using MsiCenterMod.Services.Abstractions;

namespace MsiCenterMod.Services.Hardware.Aspects;

/// <summary>
/// Áp mức hiệu năng (shift mode): ghi EC 0xD2 = 0xC0 + offset,
/// đúng như Shift.SetShiftModeValueInEC của MSI Center.
/// </summary>
public sealed class PerformanceAspect(IMsiWmiClient wmi) : IScenarioAspect
{
    public string DisplayName => "Mức hiệu năng";

    public int Order => 10;

    public Task ApplyAsync(ScenarioProfile profile, CancellationToken ct)
    {
        byte offset = profile.Performance switch
        {
            PerformanceLevel.Turbo => 4,
            PerformanceLevel.High => 0,
            PerformanceLevel.Balanced => 1,
            PerformanceLevel.Eco => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile.Performance, "Mức hiệu năng không hợp lệ."),
        };

        byte value = (byte)(MsiEcRegisters.ShiftBase + offset);
        if (!wmi.TryWrite(MsiEcRegisters.SetData, MsiEcRegisters.ShiftMode, [value]))
        {
            throw new InvalidOperationException($"Ghi shift mode 0x{value:X2} vào EC 0x{MsiEcRegisters.ShiftMode:X2} thất bại.");
        }

        return Task.CompletedTask;
    }
}
