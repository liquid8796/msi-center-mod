namespace MsiCenterMod.Services.Hardware;

/// <summary>
/// Bản đồ thanh ghi EC và method WMI — dịch ngược từ chính MSI Center
/// (API_NB_Base Module.dll / MSIWMIACPI2.dll) chạy trên máy này (GP76 Leopard 11UG, EC 17K3EMS1).
/// Mọi giá trị ở đây đều là giá trị MSI Center tự ghi — không tự chế.
/// </summary>
internal static class MsiEcRegisters
{
    // ----- Địa chỉ EC (ghi qua Set_Data: gói [địa_chỉ, giá_trị]) -----

    /// <summary>Shift mode (mức hiệu năng).</summary>
    public const byte ShiftMode = 0xD2;

    /// <summary>Cờ chế độ quạt: bit 7 = Advanced, bit 4 = Silent.</summary>
    public const byte FanFlags = 0xD4;

    /// <summary>Cooler Boost: bit 7.</summary>
    public const byte CoolerBoost = 0x98;

    // ----- Bit flags -----

    public const byte FanAdvancedBit = 0x80;
    public const byte FanSilentBit = 0x10;
    public const byte CoolerBoostBit = 0x80;

    /// <summary>
    /// Giá trị shift mode = 0xC0 (bit7: hỗ trợ shift, bit6: shift active) + offset mức hiệu năng.
    /// Turbo=+4 → 0xC4, High=+0 → 0xC0, Balanced=+1 → 0xC1, Eco=+2 → 0xC2.
    /// </summary>
    public const byte ShiftBase = 0xC0;

    // ----- Tên method WMI (class MSI_ACPI) -----

    public const string GetWmi = "Get_WMI";
    public const string GetEc = "Get_EC";
    public const string GetAp = "Get_AP";
    public const string GetFan = "Get_Fan";
    public const string SetFan = "Set_Fan";
    public const string GetTemperature = "Get_Temperature";
    public const string GetThermal = "Get_Thermal";
    public const string SetData = "Set_Data";

    // ----- Sub-index cho các method Get -----

    /// <summary>Get_AP(0) → data[2] = giá trị EC 0xD2 (shift mode hiện tại).</summary>
    public const byte ApShiftGroup = 0;

    /// <summary>Get_AP(1) → data[0] = giá trị EC 0xD4 (fan flags hiện tại).</summary>
    public const byte ApFanGroup = 1;

    /// <summary>Get_Thermal(3) → data[0] = giá trị EC 0x98 (cooler boost).</summary>
    public const byte ThermalCoolerBoostGroup = 3;

    /// <summary>Get_Fan(0) → data[0..1]=RPM CPU (word), data[2..3]=RPM GPU.</summary>
    public const byte FanRpmGroup = 0;

    /// <summary>Get_Fan/Set_Fan(1) = đường cong quạt CPU, (2) = GPU; tốc độ nằm ở data[1..6].</summary>
    public const byte FanCurveCpu = 1;
    public const byte FanCurveGpu = 2;

    /// <summary>Get_Temperature(0) → data[0]=nhiệt CPU, data[1]=nhiệt GPU.</summary>
    public const byte TemperatureCurrentGroup = 0;

    /// <summary>Get_Temperature(1|2) → ngưỡng nhiệt của đường cong quạt CPU|GPU tại data[1..6].</summary>
    public const byte TemperatureCurveCpu = 1;
    public const byte TemperatureCurveGpu = 2;

    /// <summary>Payload Set_Fan dài 8 byte (giống DataLength của MSI Center).</summary>
    public const int FanPayloadLength = 8;

    /// <summary>Công thức RPM của MSI Center: 60_000_000 / (count * 2 * 62.5).</summary>
    public static int ToRpm(int highByte, int lowByte)
    {
        int count = (highByte << 8) | lowByte;
        if (count <= 0)
        {
            return 0;
        }

        int rpm = (int)(60_000_000.0 / (count * 2 * 62.5));
        return rpm < 0 ? 0 : rpm;
    }
}
