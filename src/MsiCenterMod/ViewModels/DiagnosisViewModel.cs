using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsiCenterMod.Models;
using MsiCenterMod.Services.Abstractions;
using MsiCenterMod.Services.System;

namespace MsiCenterMod.ViewModels;

/// <summary>Một ổ đĩa hiển thị trong SSD Status.</summary>
public sealed record DiskCardViewModel(
    string Model,
    string TotalText,
    string UsedText,
    string FreeText,
    int UsedPercent,
    string TemperatureText,
    string HealthText,
    bool IsHealthy);

/// <summary>
/// Tab System Diagnosis: Battery Master (EC 0xD7) + System Checker (disk/RAM)
/// + SSD Status. Chỉ poll khi tab đang mở.
/// </summary>
public sealed partial class DiagnosisViewModel : ObservableObject
{
    private static readonly string BatteryCalibrationPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        "MSI", "MSI NBFoundation Service", "MSIBatteryCalibration.exe");

    private readonly ISystemMetricsService _metrics;
    private readonly IBatteryChargeService _battery;
    private readonly IStorageInfoService _storage;
    private readonly IMemoryCleaner _memoryCleaner;
    private readonly DispatcherTimer _timer;
    private bool _isRefreshing;
    private bool _suppressBatteryWrite;

    [ObservableProperty] private int _diskActivity;
    [ObservableProperty] private int _memoryUsage;
    [ObservableProperty] private string _freeMemoryResult = string.Empty;
    [ObservableProperty] private string _batteryStatusText = string.Empty;
    [ObservableProperty] private BatteryChargeMode _batteryMode = BatteryChargeMode.Unknown;
    [ObservableProperty] private bool _isLoadingDisks;

    public ObservableCollection<DiskCardViewModel> Disks { get; } = [];

    public bool IsBatteryCalibrationAvailable { get; } = File.Exists(BatteryCalibrationPath);

    public DiagnosisViewModel(
        ISystemMetricsService metrics,
        IBatteryChargeService battery,
        IStorageInfoService storage,
        IMemoryCleaner memoryCleaner)
    {
        _metrics = metrics;
        _battery = battery;
        _storage = storage;
        _memoryCleaner = memoryCleaner;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await RefreshMetricsAsync();
    }

    public void SetActive(bool active)
    {
        if (active)
        {
            _timer.Start();
            _ = RefreshMetricsAsync();
            _ = LoadBatteryModeAsync();
            if (Disks.Count == 0)
            {
                _ = RefreshDisksAsync();
            }
        }
        else
        {
            _timer.Stop();
        }
    }

    // ---------- Battery Master ----------

    private async Task LoadBatteryModeAsync()
    {
        try
        {
            int? percent = await _battery.ReadChargeStopPercentAsync();
            _suppressBatteryWrite = true;
            BatteryMode = percent is { } p
                ? BatteryChargeModeExtensions.FromStopPercent(p)
                : BatteryChargeMode.Unknown;
            _suppressBatteryWrite = false;

            BatteryStatusText = percent is { } cur
                ? $"Giới hạn sạc hiện tại: {cur}%"
                : "Không đọc được trạng thái pin (cần quyền Administrator).";
        }
        catch (Exception ex)
        {
            AppLogger.Error("Đọc Battery Master thất bại", ex);
        }
    }

    partial void OnBatteryModeChanged(BatteryChargeMode value)
    {
        if (_suppressBatteryWrite || value == BatteryChargeMode.Unknown)
        {
            return;
        }

        _ = ApplyBatteryModeAsync(value);
    }

    private async Task ApplyBatteryModeAsync(BatteryChargeMode mode)
    {
        bool ok = await _battery.SetModeAsync(mode);
        BatteryStatusText = ok
            ? $"✔ Đã đặt giới hạn sạc {mode.ToStopPercent()}%"
            : "✖ Đặt giới hạn sạc thất bại (xem log).";
        if (!ok)
        {
            await LoadBatteryModeAsync();
        }
    }

    [RelayCommand]
    private void RunBatteryCalibration()
    {
        try
        {
            Process.Start(new ProcessStartInfo(BatteryCalibrationPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLogger.Error("Không mở được Battery Calibration", ex);
        }
    }

    // ---------- System Checker ----------

    private async Task RefreshMetricsAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            // includeGpu: false — tab này không cần nvidia-smi (tránh đánh thức dGPU)
            SystemMetrics metrics = await _metrics.ReadAsync(includeGpu: false);
            DiskActivity = metrics.DiskActivityPercent;
            MemoryUsage = metrics.MemoryUsagePercent;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Đọc chỉ số System Checker thất bại", ex);
        }
        finally
        {
            _isRefreshing = false;
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

    [RelayCommand]
    private async Task FreeMemoryAsync()
    {
        FreeMemoryResult = "Đang giải phóng RAM…";
        try
        {
            (int count, double freedMb) = await _memoryCleaner.TrimWorkingSetsAsync();
            FreeMemoryResult = $"✔ Đã trim {count} tiến trình, giải phóng ~{freedMb:N0} MB";
        }
        catch (Exception ex)
        {
            AppLogger.Error("Giải phóng RAM thất bại", ex);
            FreeMemoryResult = "✖ Giải phóng RAM thất bại.";
        }
    }

    // ---------- SSD Status ----------

    [RelayCommand]
    private async Task RefreshDisksAsync()
    {
        if (IsLoadingDisks)
        {
            return;
        }

        IsLoadingDisks = true;
        try
        {
            IReadOnlyList<PhysicalDiskInfo> disks = await _storage.ReadDisksAsync();
            Disks.Clear();
            foreach (PhysicalDiskInfo disk in disks)
            {
                Disks.Add(new DiskCardViewModel(
                    disk.Model,
                    $"Tổng {disk.TotalGb:0} GB",
                    $"Đã dùng {disk.UsedGb:0} GB",
                    $"Trống {disk.FreeGb:0} GB",
                    disk.UsedPercent,
                    disk.TemperatureC is { } t ? $"{t} °C" : "— °C",
                    disk.IsHealthy ? "Healthy" : "Cần chú ý",
                    disk.IsHealthy));
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Đọc danh sách ổ đĩa thất bại", ex);
        }
        finally
        {
            IsLoadingDisks = false;
        }
    }
}
