using CommunityToolkit.Mvvm.ComponentModel;
using MsiCenterMod.Services.System;

namespace MsiCenterMod.ViewModels;

/// <summary>
/// Trạng thái màn hình khởi động. Tiến trình phản ánh đúng các bước khởi tạo thật
/// (WMI → cảm biến → cấu hình → giao diện), không phải thanh chạy giả.
/// </summary>
public sealed partial class SplashViewModel : ObservableObject
{
    /// <summary>0–100.</summary>
    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public string VersionText { get; } =
        $"v{typeof(SplashViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";

    /// <summary>Cập nhật một bước khởi tạo; <paramref name="statusKey"/> là khóa localization.</summary>
    public void Report(double percent, string statusKey)
    {
        Progress = Math.Clamp(percent, 0, 100);
        StatusText = Loc.Get(statusKey);
    }
}
