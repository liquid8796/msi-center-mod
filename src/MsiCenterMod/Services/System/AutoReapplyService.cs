using Microsoft.Win32;
using MsiCenterMod.Models;
using MsiCenterMod.Services.Abstractions;
using WinForms = System.Windows.Forms;

namespace MsiCenterMod.Services.System;

/// <summary>
/// Tự áp lại scenario cuối cùng — thay vai trò của MSI Center service:
///  - khi app khởi động (EC quay về mặc định sau mỗi lần boot),
///  - khi máy resume từ sleep (chờ vài giây cho EC sẵn sàng),
///  - khi đổi nguồn AC ↔ pin.
/// Chỉ hoạt động khi AutoReapplyEnabled bật trong settings.
/// </summary>
public sealed class AutoReapplyService(
    IHardwareController hardware,
    IScenarioRepository scenarios,
    ISettingsService settings) : IDisposable
{
    private WinForms.PowerLineStatus _lastPowerLine;
    private bool _started;

    public void Start(bool applyNow)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _lastPowerLine = WinForms.SystemInformation.PowerStatus.PowerLineStatus;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        if (applyNow)
        {
            _ = ReapplyAsync("khởi động app", delayMs: 0);
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Resume:
                // EC cần vài giây sau resume mới nhận lệnh ổn định (MSI Center cũng chờ).
                _ = ReapplyAsync("resume từ sleep", delayMs: 3000);
                break;

            case PowerModes.StatusChange:
                WinForms.PowerLineStatus current = WinForms.SystemInformation.PowerStatus.PowerLineStatus;
                if (current != _lastPowerLine)
                {
                    _lastPowerLine = current;
                    string source = current == WinForms.PowerLineStatus.Online ? "cắm sạc" : "rút sạc";
                    _ = ReapplyAsync($"đổi nguồn ({source})", delayMs: 1000);
                }

                break;
        }
    }

    private async Task ReapplyAsync(string reason, int delayMs)
    {
        try
        {
            if (!settings.Current.AutoReapplyEnabled || !hardware.IsOperational)
            {
                return;
            }

            if (settings.Current.LastAppliedScenarioId is not { } id)
            {
                return;
            }

            ScenarioProfile? profile = scenarios.LoadAll().FirstOrDefault(p => p.Id == id);
            if (profile is null)
            {
                return;
            }

            if (delayMs > 0)
            {
                await Task.Delay(delayMs).ConfigureAwait(false);
            }

            ScenarioApplyResult result = await hardware.ApplyScenarioAsync(profile).ConfigureAwait(false);
            AppLogger.Info(result.Success
                ? $"Tự áp lại \"{profile.Name}\" ({reason}) thành công."
                : $"Tự áp lại \"{profile.Name}\" ({reason}) LỖI: {string.Join("; ", result.Errors)}");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Tự áp lại scenario ({reason}) thất bại", ex);
        }
    }

    public void Dispose()
    {
        if (_started)
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            _started = false;
        }
    }
}
