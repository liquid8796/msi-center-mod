namespace MsiCenterMod.Models;

/// <summary>
/// Power mode overlay của Windows (slider hiệu năng trong Settings/Battery flyout),
/// áp qua API PowerSetUserConfiguredAC/DCPowerMode giống MSI Center.
/// </summary>
public enum PowerOverlayMode
{
    /// <summary>Không thay đổi power mode hiện tại của Windows.</summary>
    None,

    /// <summary>Best Performance.</summary>
    BestPerformance,

    /// <summary>Balanced (mặc định của Windows).</summary>
    Balanced,

    /// <summary>Best Power Efficiency.</summary>
    BestEfficiency,
}
