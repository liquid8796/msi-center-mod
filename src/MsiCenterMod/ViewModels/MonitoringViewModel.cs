using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsiCenterMod.Models;
using MsiCenterMod.Services.Abstractions;
using MsiCenterMod.Services.System;

namespace MsiCenterMod.ViewModels;

/// <summary>
/// ViewModel tab Hardware Monitoring. Chỉ poll khi tab đang mở (SetActive) —
/// vừa tiết kiệm CPU vừa tránh nvidia-smi giữ dGPU thức gây tốn pin.
/// </summary>
public sealed partial class MonitoringViewModel : ObservableObject
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly ISystemMetricsService _metrics;
    private readonly IHardwareController _hardware;
    private readonly DispatcherTimer _timer;
    private bool _isRefreshing;

    public string CpuName { get; }

    // Donut + thanh %
    [ObservableProperty] private double _cpuUsage;
    [ObservableProperty] private string _cpuUsageText = "0";
    [ObservableProperty] private double _gpuUsage;
    [ObservableProperty] private string _gpuUsageText = "0";
    [ObservableProperty] private int _memoryUsage;
    [ObservableProperty] private int _diskActivity;

    // Bảng thông tin bên phải
    [ObservableProperty] private string _gpuName = "—";
    [ObservableProperty] private string _gpuClockText = "—";
    [ObservableProperty] private string _vramClockText = "—";
    [ObservableProperty] private string _gpuTempText = "—";
    [ObservableProperty] private string _cpuTempText = "—";
    [ObservableProperty] private string _ramText = "—";
    [ObservableProperty] private string _ssdAvailableText = "—";
    [ObservableProperty] private int _ssdUsedPercent;
    [ObservableProperty] private string _fan1Text = "—";
    [ObservableProperty] private string _fan2Text = "—";
    [ObservableProperty] private string _lanText = "—";
    [ObservableProperty] private string _wifiText = "—";
    [ObservableProperty] private string _powerPlanText = "—";

    public MonitoringViewModel(ISystemMetricsService metrics, IHardwareController hardware)
    {
        _metrics = metrics;
        _hardware = hardware;
        CpuName = metrics.CpuName;

        _timer = new DispatcherTimer { Interval = PollInterval };
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    /// <summary>Bật/tắt vòng poll theo trạng thái tab (MainViewModel gọi khi đổi tab).</summary>
    public void SetActive(bool active)
    {
        if (active)
        {
            _timer.Start();
            _ = RefreshAsync();
        }
        else
        {
            _timer.Stop();
        }
    }

    [RelayCommand]
    private void CleanDisk()
    {
        try
        {
            Process.Start(new ProcessStartInfo("cleanmgr.exe") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLogger.Error("Không mở được Disk Cleanup", ex);
        }
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            SystemMetrics metrics = await _metrics.ReadAsync();
            HardwareStatus? ec = _hardware.IsOperational
                ? await _hardware.ReadStatusAsync()
                : null;

            CpuUsage = metrics.CpuUsagePercent;
            CpuUsageText = Math.Round(metrics.CpuUsagePercent).ToString("0");
            GpuUsage = metrics.GpuUsagePercent ?? 0;
            GpuUsageText = Math.Round(metrics.GpuUsagePercent ?? 0).ToString("0");
            MemoryUsage = metrics.MemoryUsagePercent;
            DiskActivity = metrics.DiskActivityPercent;

            GpuName = metrics.GpuName ?? "—";
            GpuClockText = metrics.GpuCoreClockMhz is { } core ? $"{core} MHz" : "—";
            VramClockText = metrics.GpuVramClockMhz is { } vram ? $"{vram} MHz" : "—";
            RamText = metrics.TotalRamGb > 0 ? $"{metrics.TotalRamGb:0} GB" : "—";
            SsdAvailableText = metrics.SsdAvailableGb > 0
                ? Loc.Format("S.Mon.SsdAvailable", metrics.SsdAvailableGb)
                : "—";
            SsdUsedPercent = metrics.SsdUsedPercent;
            LanText = FormatBytesPerSec(metrics.LanBytesPerSec);
            WifiText = FormatBytesPerSec(metrics.WifiBytesPerSec);
            PowerPlanText = string.IsNullOrWhiteSpace(metrics.PowerPlanName) ? "—" : metrics.PowerPlanName;

            // Nhiệt độ: ưu tiên cảm biến GPU của nvidia-smi, fallback EC; CPU lấy từ EC.
            int? gpuTemp = metrics.GpuTemperatureC ?? (ec?.GpuTemperature > 0 ? ec.GpuTemperature : null);
            GpuTempText = gpuTemp is { } gt ? $"{gt} °C" : "—";
            CpuTempText = ec?.CpuTemperature > 0 ? $"{ec.CpuTemperature} °C" : "—";
            Fan1Text = ec is not null ? $"{ec.CpuFanRpm:N0} RPM" : "—";
            Fan2Text = ec is not null ? $"{ec.GpuFanRpm:N0} RPM" : "—";
        }
        catch (Exception ex)
        {
            AppLogger.Error("Đọc chỉ số monitoring thất bại", ex);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private static string FormatBytesPerSec(double bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / 1024 / 1024:0.0} MB/s",
        >= 1024 => $"{bytes / 1024:0.0} KB/s",
        _ => $"{bytes:0} Bytes/s",
    };
}
