using CommunityToolkit.Mvvm.ComponentModel;
using MsiCenterMod.Models;

namespace MsiCenterMod.ViewModels;

/// <summary>Trạng thái phần cứng hiển thị ở thanh dưới cùng, làm mới định kỳ.</summary>
public sealed partial class StatusViewModel : ObservableObject
{
    [ObservableProperty]
    private string _cpuText = "CPU —";

    [ObservableProperty]
    private string _gpuText = "GPU —";

    [ObservableProperty]
    private string _modeText = "Chế độ: —";

    public void Update(HardwareStatus status)
    {
        CpuText = $"CPU {status.CpuTemperature}°C · {status.CpuFanRpm:N0} RPM";
        GpuText = $"GPU {status.GpuTemperature}°C · {status.GpuFanRpm:N0} RPM";

        string perf = status.Performance is { } level
            ? ScenarioViewModel.PerformanceLabel(level)
            : $"0x{status.ShiftModeRaw:X2}";
        ModeText = $"Chế độ: {perf} / {ScenarioViewModel.FanLabel(status.CurrentFanMode)}";
    }
}
