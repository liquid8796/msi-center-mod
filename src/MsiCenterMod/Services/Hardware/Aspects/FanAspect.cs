using MsiCenterMod.Models;
using MsiCenterMod.Services.Abstractions;

namespace MsiCenterMod.Services.Hardware.Aspects;

/// <summary>
/// Áp chế độ quạt. Tái hiện đúng trình tự UserScenario.setFan/Adv_Fan của MSI Center:
///  - Auto:        tắt Cooler Boost, xóa bit Advanced + Silent của EC 0xD4.
///  - Silent:      tắt Cooler Boost, xóa bit Advanced, bật bit Silent.
///  - Advanced:    tắt Cooler Boost, ghi 6 điểm tốc độ vào Set_Fan(1|2), bật bit Advanced.
///  - CoolerBoost: đưa quạt về Auto rồi bật bit 7 của EC 0x98.
/// </summary>
public sealed class FanAspect(IMsiWmiClient wmi) : IScenarioAspect
{
    public string DisplayName => "Chế độ quạt";

    public int Order => 20;

    public Task ApplyAsync(ScenarioProfile profile, CancellationToken ct)
    {
        switch (profile.FanMode)
        {
            case FanMode.Auto:
                SetCoolerBoost(false);
                WriteFanFlags(advanced: false, silent: false);
                break;

            case FanMode.Silent:
                SetCoolerBoost(false);
                WriteFanFlags(advanced: false, silent: true);
                break;

            case FanMode.Advanced:
                SetCoolerBoost(false);
                WriteCurve(MsiEcRegisters.FanCurveCpu, profile.CpuFanCurve, "CPU");
                WriteCurve(MsiEcRegisters.FanCurveGpu, profile.GpuFanCurve, "GPU");
                WriteFanFlags(advanced: true, silent: false);
                break;

            case FanMode.CoolerBoost:
                WriteFanFlags(advanced: false, silent: false);
                SetCoolerBoost(true);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(profile), profile.FanMode, "Chế độ quạt không hợp lệ.");
        }

        return Task.CompletedTask;
    }

    /// <summary>Đọc-sửa-ghi EC 0xD4, chỉ động vào bit Advanced (7) và Silent (4).</summary>
    private void WriteFanFlags(bool advanced, bool silent)
    {
        if (!wmi.TryReadFanFlags(out byte flags))
        {
            throw new InvalidOperationException("Đọc thanh ghi quạt (EC 0xD4) thất bại.");
        }

        flags = advanced
            ? (byte)(flags | MsiEcRegisters.FanAdvancedBit)
            : (byte)(flags & ~MsiEcRegisters.FanAdvancedBit);
        flags = silent
            ? (byte)(flags | MsiEcRegisters.FanSilentBit)
            : (byte)(flags & ~MsiEcRegisters.FanSilentBit);

        if (!wmi.TryWriteFanFlags(flags))
        {
            throw new InvalidOperationException($"Ghi thanh ghi quạt (EC 0xD4 = 0x{flags:X2}) thất bại.");
        }
    }

    private void SetCoolerBoost(bool enabled)
    {
        if (!wmi.TrySetCoolerBoost(enabled))
        {
            throw new InvalidOperationException("Ghi Cooler Boost (EC 0x98) thất bại.");
        }
    }

    /// <summary>
    /// Ghi đường cong quạt: đọc gói 8 byte hiện tại từ Get_Fan, thay byte 1..6
    /// bằng 6 tốc độ mới rồi ghi lại — giữ nguyên byte 0 và 7 như MSI Center.
    /// </summary>
    private void WriteCurve(byte target, int[] speeds, string label)
    {
        if (speeds is not { Length: FanCurve.PointCount })
        {
            throw new ArgumentException($"Đường cong quạt {label} phải có đúng {FanCurve.PointCount} điểm.");
        }

        if (!wmi.TryRead(MsiEcRegisters.GetFan, target, out byte[] data)
            || data.Length < MsiEcRegisters.FanPayloadLength)
        {
            throw new InvalidOperationException($"Đọc đường cong quạt {label} (Get_Fan {target}) thất bại.");
        }

        var payload = new byte[MsiEcRegisters.FanPayloadLength];
        Array.Copy(data, payload, MsiEcRegisters.FanPayloadLength);
        for (int i = 0; i < FanCurve.PointCount; i++)
        {
            payload[i + 1] = (byte)FanCurve.Clamp(speeds[i]);
        }

        if (!wmi.TryWrite(MsiEcRegisters.SetFan, target, payload))
        {
            throw new InvalidOperationException($"Ghi đường cong quạt {label} (Set_Fan {target}) thất bại.");
        }
    }
}
