using System.Runtime.InteropServices;
using MsiCenterMod.Models;
using MsiCenterMod.Services.Abstractions;

namespace MsiCenterMod.Services.System;

/// <summary>
/// Đặt Windows power mode overlay qua Powrprof.dll —
/// cùng API và cùng GUID mà MSI Center sử dụng.
/// </summary>
public sealed class PowerOverlayService : IPowerOverlayService
{
    // GUID overlay chuẩn của Windows (đối chiếu từ UserScenario.cs của MSI Center).
    private static readonly Guid BestPerformanceGuid = new("ded574b5-45a0-4f42-8737-46345c09c238");
    private static readonly Guid BestEfficiencyGuid = new("961cc777-2547-4f9d-8174-7d86181b8a7a");
    private static readonly Guid BalancedGuid = Guid.Empty; // GUID rỗng = overlay mặc định

    public bool Apply(PowerOverlayMode mode)
    {
        if (mode == PowerOverlayMode.None)
        {
            return true;
        }

        Guid overlay = mode switch
        {
            PowerOverlayMode.BestPerformance => BestPerformanceGuid,
            PowerOverlayMode.BestEfficiency => BestEfficiencyGuid,
            _ => BalancedGuid,
        };

        // Áp cho cả khi cắm sạc (AC) lẫn chạy pin (DC), giống MSI Center.
        uint acResult = PowerSetUserConfiguredACPowerMode(ref overlay);
        uint dcResult = PowerSetUserConfiguredDCPowerMode(ref overlay);
        return acResult == 0 && dcResult == 0;
    }

    [DllImport("Powrprof.dll", SetLastError = true)]
    private static extern uint PowerSetUserConfiguredACPowerMode(ref Guid powerModeGuid);

    [DllImport("Powrprof.dll", SetLastError = true)]
    private static extern uint PowerSetUserConfiguredDCPowerMode(ref Guid powerModeGuid);
}
