using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MsiCenterMod.Models;
using MsiCenterMod.Services.Abstractions;

namespace MsiCenterMod.ViewModels;

/// <summary>
/// ViewModel chính: quản lý danh sách scenario, editor, nút Áp dụng
/// và vòng lặp làm mới trạng thái phần cứng.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private const string MsiCenterAiCoolingKey =
        @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\MSI\MSI Center\Component\Base Module\User Scenario";

    private readonly IHardwareController _hardware;
    private readonly IScenarioRepository _repository;
    private readonly DispatcherTimer _statusTimer;
    private bool _isApplying;

    public ObservableCollection<ScenarioViewModel> Scenarios { get; } = [];

    public StatusViewModel Status { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(DeleteScenarioCommand), nameof(DuplicateScenarioCommand))]
    private ScenarioViewModel? _selectedScenario;

    [ObservableProperty]
    private string _applyBannerText = string.Empty;

    [ObservableProperty]
    private bool _isApplySuccess;

    /// <summary>Phần cứng sẵn sàng — điều khiển được thật.</summary>
    public bool IsHardwareReady => _hardware.IsOperational;

    public bool ShowHardwareWarning => !_hardware.IsOperational;

    public string HardwareWarningText =>
        _hardware.UnavailableReason ?? string.Empty;

    /// <summary>Cảnh báo khi MSI Center đang bật AI Cooling (sẽ ghi đè cấu hình quạt).</summary>
    public bool ShowAiCoolingWarning { get; }

    public string EcInfoText { get; }

    public string StorePathText { get; }

    public string VersionText { get; } =
        $"v{typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";

    public MainViewModel(IHardwareController hardware, IScenarioRepository repository)
    {
        _hardware = hardware;
        _repository = repository;

        EcInfoText = string.IsNullOrEmpty(hardware.EcFirmwareInfo)
            ? "EC: không xác định"
            : $"EC: {hardware.EcFirmwareInfo}";
        StorePathText = repository.StorePath;
        ShowAiCoolingWarning = hardware.IsOperational && ReadAiCoolingEnabled();

        foreach (ScenarioProfile profile in repository.LoadAll())
        {
            AttachScenario(new ScenarioViewModel(profile));
        }

        SelectedScenario = Scenarios.FirstOrDefault();

        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2),
        };
        _statusTimer.Tick += async (_, _) => await RefreshStatusAsync();

        if (_hardware.IsOperational)
        {
            _statusTimer.Start();
            _ = InitializeFromHardwareAsync();
        }
    }

    // ---------- Commands ----------

    [RelayCommand]
    private void AddScenario()
    {
        var vm = new ScenarioViewModel(new ScenarioProfile
        {
            Name = $"Scenario {Scenarios.Count + 1}",
        });
        AttachScenario(vm);
        SelectedScenario = vm;
        Save();
    }

    private bool HasSelection() => SelectedScenario is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DuplicateScenario()
    {
        if (SelectedScenario is null)
        {
            return;
        }

        ScenarioProfile copy = SelectedScenario.Profile.Clone();
        copy.Name = $"{copy.Name} (bản sao)";
        var vm = new ScenarioViewModel(copy);
        AttachScenario(vm, insertAfter: SelectedScenario);
        SelectedScenario = vm;
        Save();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteScenario()
    {
        if (SelectedScenario is null)
        {
            return;
        }

        var result = global::System.Windows.MessageBox.Show(
            $"Xóa scenario \"{SelectedScenario.Name}\"?",
            "MSI Center Mod",
            global::System.Windows.MessageBoxButton.YesNo,
            global::System.Windows.MessageBoxImage.Question);
        if (result != global::System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        int index = Scenarios.IndexOf(SelectedScenario);
        SelectedScenario.Edited -= OnScenarioEdited;
        Scenarios.Remove(SelectedScenario);
        SelectedScenario = Scenarios.Count > 0
            ? Scenarios[Math.Min(index, Scenarios.Count - 1)]
            : null;
        Save();
    }

    private bool CanApply() => SelectedScenario is not null && IsHardwareReady && !_isApplying;

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        if (SelectedScenario is null)
        {
            return;
        }

        _isApplying = true;
        ApplyCommand.NotifyCanExecuteChanged();
        ApplyBannerText = $"Đang áp dụng \"{SelectedScenario.Name}\"…";
        IsApplySuccess = false;

        try
        {
            ScenarioApplyResult result = await _hardware.ApplyScenarioAsync(SelectedScenario.Profile);
            if (result.Success)
            {
                IsApplySuccess = true;
                ApplyBannerText = $"✔ Đã áp dụng \"{SelectedScenario.Name}\" lúc {DateTime.Now:HH:mm:ss}";
            }
            else
            {
                IsApplySuccess = false;
                ApplyBannerText = "✖ " + string.Join(" · ", result.Errors);
            }
        }
        finally
        {
            _isApplying = false;
            ApplyCommand.NotifyCanExecuteChanged();
            await RefreshStatusAsync();
        }
    }

    /// <summary>Áp scenario từ menu tray (không cần chọn trong UI).</summary>
    public async Task ApplyScenarioAsync(ScenarioViewModel scenario)
    {
        SelectedScenario = scenario;
        if (ApplyCommand.CanExecute(null))
        {
            await ApplyCommand.ExecuteAsync(null);
        }
    }

    // ---------- Nội bộ ----------

    private void AttachScenario(ScenarioViewModel vm, ScenarioViewModel? insertAfter = null)
    {
        vm.Edited += OnScenarioEdited;
        if (insertAfter is not null && Scenarios.Contains(insertAfter))
        {
            Scenarios.Insert(Scenarios.IndexOf(insertAfter) + 1, vm);
        }
        else
        {
            Scenarios.Add(vm);
        }
    }

    private void OnScenarioEdited(object? sender, EventArgs e) => Save();

    private void Save() => _repository.SaveAll(Scenarios.Select(s => s.Profile));

    private async Task RefreshStatusAsync()
    {
        if (!_hardware.IsOperational || _isApplying)
        {
            return;
        }

        try
        {
            HardwareStatus? status = await _hardware.ReadStatusAsync();
            if (status is not null)
            {
                Status.Update(status);
            }
        }
        catch (Exception ex)
        {
            // Timer tick là async void phía trên — tuyệt đối không để exception lọt ra.
            Services.System.AppLogger.Error("Đọc trạng thái phần cứng thất bại", ex);
        }
    }

    /// <summary>Đọc ngưỡng nhiệt thật từ EC để gắn nhãn cho các slider quạt.</summary>
    private async Task InitializeFromHardwareAsync()
    {
        try
        {
            await RefreshStatusAsync();

            FanCurve? cpu = await _hardware.ReadFanCurveAsync(FanTarget.Cpu);
            FanCurve? gpu = await _hardware.ReadFanCurveAsync(FanTarget.Gpu);
            if (cpu is null || gpu is null)
            {
                return;
            }

            foreach (ScenarioViewModel scenario in Scenarios)
            {
                scenario.UpdateTemperatureLabels(cpu.Temperatures, gpu.Temperatures);
            }
        }
        catch (Exception ex)
        {
            // Chạy fire-and-forget lúc khởi động — lỗi chỉ ghi log, không phá app.
            Services.System.AppLogger.Error("Khởi tạo dữ liệu từ EC thất bại", ex);
        }
    }

    private static bool ReadAiCoolingEnabled()
    {
        try
        {
            // "Intelligent": 1 = AI Cooling, 2 = Smart Auto — cả hai đều tự ghi đè chế độ quạt.
            object? value = Registry.GetValue(MsiCenterAiCoolingKey, "Intelligent", 0);
            return value is int mode && mode is 1 or 2;
        }
        catch
        {
            return false;
        }
    }
}
