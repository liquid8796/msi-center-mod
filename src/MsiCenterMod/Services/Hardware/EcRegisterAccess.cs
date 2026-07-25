using MsiCenterMod.Services.Abstractions;

namespace MsiCenterMod.Services.Hardware;

/// <summary>
/// Helper dùng chung cho các thao tác đọc-sửa-ghi thanh ghi EC,
/// tái hiện đúng trình tự MSI Center thực hiện.
/// </summary>
internal static class EcRegisterAccess
{
    /// <summary>Đọc giá trị EC 0xD4 (fan flags) qua Get_AP(1).</summary>
    public static bool TryReadFanFlags(this IMsiWmiClient wmi, out byte value)
    {
        value = 0;
        if (!wmi.TryRead(MsiEcRegisters.GetAp, MsiEcRegisters.ApFanGroup, out byte[] data) || data.Length < 1)
        {
            return false;
        }

        value = data[0];
        return true;
    }

    /// <summary>Ghi EC 0xD4 (fan flags) qua Set_Data.</summary>
    public static bool TryWriteFanFlags(this IMsiWmiClient wmi, byte value)
        => wmi.TryWrite(MsiEcRegisters.SetData, MsiEcRegisters.FanFlags, [value]);

    /// <summary>Đọc giá trị EC 0x98 (cooler boost) qua Get_Thermal(3).</summary>
    public static bool TryReadCoolerBoost(this IMsiWmiClient wmi, out byte value)
    {
        value = 0;
        if (!wmi.TryRead(MsiEcRegisters.GetThermal, MsiEcRegisters.ThermalCoolerBoostGroup, out byte[] data)
            || data.Length < 1)
        {
            return false;
        }

        value = data[0];
        return true;
    }

    /// <summary>Bật/tắt Cooler Boost (bit 7 của EC 0x98), giữ nguyên các bit còn lại.</summary>
    public static bool TrySetCoolerBoost(this IMsiWmiClient wmi, bool enabled)
    {
        if (!wmi.TryReadCoolerBoost(out byte current))
        {
            return false;
        }

        byte next = enabled
            ? (byte)(current | MsiEcRegisters.CoolerBoostBit)
            : (byte)(current & ~MsiEcRegisters.CoolerBoostBit);

        return wmi.TryWrite(MsiEcRegisters.SetData, MsiEcRegisters.CoolerBoost, [next]);
    }
}
