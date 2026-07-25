using CommunityToolkit.Mvvm.ComponentModel;
using MsiCenterMod.Models;
using MsiCenterMod.Services.System;

namespace MsiCenterMod.ViewModels;

/// <summary>Trạng thái phần cứng hiển thị ở thanh dưới cùng, làm mới định kỳ.</summary>
public sealed partial class StatusViewModel : ObservableObject
{
    private HardwareStatus? _lastStatus;

    [ObservableProperty]
    private string _cpuText = "CPU —";

    [ObservableProperty]
    private string _gpuText = "GPU —";

    [ObservableProperty]
    private string _modeText;

    public StatusViewModel()
    {
        _modeText = Loc.Get("S.Status.ModeIdle");
    }

    public void Update(HardwareStatus status)
    {
        _lastStatus = status;
        Render();
    }

    /// <summary>Render lại bằng dữ liệu gần nhất sau khi đổi ngôn ngữ.</summary>
    public void RefreshLocalization()
    {
        if (_lastStatus is null)
        {
            ModeText = Loc.Get("S.Status.ModeIdle");
            return;
        }

        Render();
    }

    private void Render()
    {
        if (_lastStatus is not { } status)
        {
            return;
        }

        CpuText = $"CPU {status.CpuTemperature}°C · {status.CpuFanRpm:N0} RPM";
        GpuText = $"GPU {status.GpuTemperature}°C · {status.GpuFanRpm:N0} RPM";

        string perf = status.Performance is { } level
            ? ScenarioViewModel.PerformanceLabel(level)
            : $"0x{status.ShiftModeRaw:X2}";
        ModeText = Loc.Format("S.Status.Mode", perf, ScenarioViewModel.FanLabel(status.CurrentFanMode));
    }
}
