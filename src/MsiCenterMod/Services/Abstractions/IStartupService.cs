namespace MsiCenterMod.Services.Abstractions;

/// <summary>
/// Quản lý khởi động cùng Windows. Dùng Task Scheduler (RunLevel Highest)
/// thay vì Run key vì app cần quyền admin — Run key sẽ bị UAC chặn mỗi lần logon.
/// </summary>
public interface IStartupService
{
    bool IsEnabled();

    bool TrySetEnabled(bool enabled, out string error);
}
