using MsiCenterMod.Models;

namespace MsiCenterMod.Services.Abstractions;

/// <summary>
/// Một "khía cạnh" của scenario (shift mode, quạt, power overlay, ...).
/// Đây là điểm mở rộng chính: thêm tính năng mới (TDP, giới hạn sạc pin, đèn phím, ...)
/// chỉ cần viết một aspect mới và đăng ký vào DI — không sửa code hiện có (Open/Closed).
/// </summary>
public interface IScenarioAspect
{
    /// <summary>Tên hiển thị trong thông báo lỗi, ví dụ "Mức hiệu năng".</summary>
    string DisplayName { get; }

    /// <summary>Thứ tự chạy (nhỏ chạy trước). Shift mode chạy trước quạt, giống MSI Center.</summary>
    int Order { get; }

    /// <summary>Áp phần cấu hình của aspect này. Ném exception nếu thất bại.</summary>
    Task ApplyAsync(ScenarioProfile profile, CancellationToken ct);
}
