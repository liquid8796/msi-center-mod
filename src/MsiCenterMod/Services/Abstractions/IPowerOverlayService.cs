using MsiCenterMod.Models;

namespace MsiCenterMod.Services.Abstractions;

/// <summary>Đặt Windows power mode overlay (slider hiệu năng của Windows).</summary>
public interface IPowerOverlayService
{
    /// <summary>Áp overlay cho cả AC và DC. Trả về false nếu API Windows từ chối.</summary>
    bool Apply(PowerOverlayMode mode);
}
