using MsiCenterMod.Models;
using MsiCenterMod.Services.Abstractions;

namespace MsiCenterMod.Services.Hardware;

/// <summary>
/// Battery Master qua EC 0xD7 — tái hiện đúng WriteECBatteryMode_WMIACPI_2
/// của MSI Center (API_NB_System Diagnosis.dll):
///   đọc Get_Data(0xD7) → giữ bit 7, thay 7 bit thấp = % dừng sạc → Set_Data(0xD7).
/// </summary>
public sealed class BatteryChargeService(IMsiWmiClient wmi) : IBatteryChargeService
{
    /// <summary>Thanh ghi giới hạn sạc: bit 7 = cờ của EC, bit 0-6 = % dừng sạc.</summary>
    private const byte BatteryChargeRegister = 0xD7;

    private const string GetData = "Get_Data";

    public Task<int?> ReadChargeStopPercentAsync(CancellationToken ct = default)
    {
        if (!wmi.IsAvailable)
        {
            return Task.FromResult<int?>(null);
        }

        return Task.Run<int?>(() =>
        {
            if (!wmi.TryRead(GetData, BatteryChargeRegister, out byte[] data) || data.Length < 1)
            {
                return null;
            }

            return data[0] & 0x7F;
        }, ct);
    }

    public Task<bool> SetModeAsync(BatteryChargeMode mode, CancellationToken ct = default)
    {
        if (!wmi.IsAvailable || mode == BatteryChargeMode.Unknown)
        {
            return Task.FromResult(false);
        }

        return Task.Run(() =>
        {
            if (!wmi.TryRead(GetData, BatteryChargeRegister, out byte[] data) || data.Length < 1)
            {
                return false;
            }

            byte current = data[0];
            byte next = (byte)((current & 0x80) | mode.ToStopPercent());
            return wmi.TryWrite(MsiEcRegisters.SetData, BatteryChargeRegister, [next]);
        }, ct);
    }
}
