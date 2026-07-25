using System.Diagnostics;
using System.Security.Principal;
using MsiCenterMod.Services.Abstractions;

namespace MsiCenterMod.Services.System;

/// <summary>Kiểm tra quyền admin và tự khởi động lại có UAC khi cần.</summary>
public sealed class ElevationService : IElevationService
{
    public bool IsElevated { get; } = ComputeIsElevated();

    private static bool ComputeIsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public bool TryRelaunchElevated()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas", // hiện hộp thoại UAC
            });
            return true;
        }
        catch
        {
            // Người dùng bấm No trên UAC (hoặc policy chặn) → tiếp tục chạy chế độ chỉ xem.
            return false;
        }
    }
}
