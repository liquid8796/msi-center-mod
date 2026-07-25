using MsiCenterMod.Models;

namespace MsiCenterMod.Services.Abstractions;

/// <summary>
/// Facade cấp cao cho mọi thao tác phần cứng. ViewModel chỉ dùng interface này,
/// không đụng tới WMI trực tiếp — dễ test và dễ thay thế backend.
/// </summary>
public interface IHardwareController
{
    /// <summary>Phần cứng sẵn sàng nhận lệnh (đủ quyền admin + WMI hợp lệ).</summary>
    bool IsOperational { get; }

    /// <summary>Mô tả lý do không hoạt động (hiện lên UI khi <see cref="IsOperational"/> = false).</summary>
    string? UnavailableReason { get; }

    string EcFirmwareInfo { get; }

    Task<HardwareStatus?> ReadStatusAsync(CancellationToken ct = default);

    /// <summary>Đọc đường cong quạt hiện tại từ EC (kèm ngưỡng nhiệt để hiển thị).</summary>
    Task<FanCurve?> ReadFanCurveAsync(FanTarget target, CancellationToken ct = default);

    /// <summary>Áp toàn bộ scenario: chạy tuần tự các <see cref="IScenarioAspect"/> đã đăng ký.</summary>
    Task<ScenarioApplyResult> ApplyScenarioAsync(ScenarioProfile profile, CancellationToken ct = default);
}

public enum FanTarget
{
    Cpu,
    Gpu,
}

/// <summary>Kết quả áp scenario — liệt kê từng bước để UI báo lỗi chi tiết.</summary>
public sealed record ScenarioApplyResult(bool Success, IReadOnlyList<string> Errors)
{
    public static ScenarioApplyResult Ok { get; } = new(true, []);
}
