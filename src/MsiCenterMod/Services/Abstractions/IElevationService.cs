namespace MsiCenterMod.Services.Abstractions;

/// <summary>Kiểm tra và xử lý nâng quyền Administrator (bắt buộc để gọi MSI_ACPI).</summary>
public interface IElevationService
{
    bool IsElevated { get; }

    /// <summary>
    /// Khởi động lại chính app với quyền admin (hiện UAC).
    /// Trả về true nếu tiến trình mới đã chạy (tiến trình hiện tại nên thoát).
    /// </summary>
    bool TryRelaunchElevated();
}
