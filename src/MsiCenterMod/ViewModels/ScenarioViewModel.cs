using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MsiCenterMod.Models;

namespace MsiCenterMod.ViewModels;

/// <summary>
/// ViewModel bọc một <see cref="ScenarioProfile"/>: mọi thay đổi từ UI được ghi thẳng
/// vào profile và phát sự kiện <see cref="Edited"/> để MainViewModel tự động lưu.
/// </summary>
public sealed partial class ScenarioViewModel : ObservableObject
{
    /// <summary>Ngưỡng nhiệt mặc định của máy (từ Default_Temp trong registry MSI Center) —
    /// dùng khi chưa đọc được ngưỡng thật từ EC.</summary>
    private static readonly int[] FallbackCpuTemps = [0, 50, 56, 62, 70, 75];
    private static readonly int[] FallbackGpuTemps = [0, 55, 60, 65, 70, 75];

    public ScenarioProfile Profile { get; }

    /// <summary>Phát khi người dùng sửa bất kỳ thuộc tính nào (để autosave).</summary>
    public event EventHandler? Edited;

    public ObservableCollection<FanPointViewModel> CpuPoints { get; } = [];

    public ObservableCollection<FanPointViewModel> GpuPoints { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private string _name;

    [ObservableProperty]
    private string _glyph;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private PerformanceLevel _performance;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary), nameof(IsAdvancedFan))]
    private FanMode _fanMode;

    [ObservableProperty]
    private PowerOverlayMode _powerOverlay;

    public bool IsAdvancedFan => FanMode == FanMode.Advanced;

    /// <summary>Dòng mô tả ngắn trên card scenario, ví dụ "Turbo · Quạt tùy chỉnh".</summary>
    public string Summary => $"{PerformanceLabel(Performance)} · {FanLabel(FanMode)}";

    public ScenarioViewModel(ScenarioProfile profile)
    {
        Profile = profile;
        _name = profile.Name;
        _glyph = profile.Glyph;
        _performance = profile.Performance;
        _fanMode = profile.FanMode;
        _powerOverlay = profile.PowerOverlay;

        RebuildFanPoints(FallbackCpuTemps, FallbackGpuTemps);
    }

    /// <summary>Cập nhật nhãn nhiệt độ theo ngưỡng thật đọc từ EC (giữ nguyên tốc độ).</summary>
    public void UpdateTemperatureLabels(int[] cpuTemps, int[] gpuTemps)
        => RebuildFanPoints(cpuTemps, gpuTemps);

    private void RebuildFanPoints(int[] cpuTemps, int[] gpuTemps)
    {
        Rebuild(CpuPoints, cpuTemps, Profile.CpuFanCurve, isCpu: true);
        Rebuild(GpuPoints, gpuTemps, Profile.GpuFanCurve, isCpu: false);

        void Rebuild(ObservableCollection<FanPointViewModel> points, int[] temps, int[] speeds, bool isCpu)
        {
            foreach (FanPointViewModel old in points)
            {
                old.PropertyChanged -= OnFanPointChanged;
            }

            points.Clear();
            for (int i = 0; i < FanCurve.PointCount; i++)
            {
                var point = new FanPointViewModel(temps.Length > i ? temps[i] : 0, speeds[i]);
                point.PropertyChanged += OnFanPointChanged;
                points.Add(point);
            }
        }
    }

    private void OnFanPointChanged(object? sender, global::System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FanPointViewModel.Speed))
        {
            return;
        }

        for (int i = 0; i < FanCurve.PointCount; i++)
        {
            Profile.CpuFanCurve[i] = CpuPoints[i].Speed;
            Profile.GpuFanCurve[i] = GpuPoints[i].Speed;
        }

        Edited?.Invoke(this, EventArgs.Empty);
    }

    partial void OnNameChanged(string value)
    {
        Profile.Name = string.IsNullOrWhiteSpace(value) ? "Scenario chưa đặt tên" : value.Trim();
        Edited?.Invoke(this, EventArgs.Empty);
    }

    partial void OnGlyphChanged(string value)
    {
        Profile.Glyph = value;
        Edited?.Invoke(this, EventArgs.Empty);
    }

    partial void OnPerformanceChanged(PerformanceLevel value)
    {
        Profile.Performance = value;
        Edited?.Invoke(this, EventArgs.Empty);
    }

    partial void OnFanModeChanged(FanMode value)
    {
        Profile.FanMode = value;
        Edited?.Invoke(this, EventArgs.Empty);
    }

    partial void OnPowerOverlayChanged(PowerOverlayMode value)
    {
        Profile.PowerOverlay = value;
        Edited?.Invoke(this, EventArgs.Empty);
    }

    public static string PerformanceLabel(PerformanceLevel level) => level switch
    {
        PerformanceLevel.Turbo => "Turbo",
        PerformanceLevel.High => "Cao",
        PerformanceLevel.Balanced => "Cân bằng",
        PerformanceLevel.Eco => "Tiết kiệm",
        _ => level.ToString(),
    };

    public static string FanLabel(FanMode mode) => mode switch
    {
        FanMode.Auto => "Quạt tự động",
        FanMode.Silent => "Quạt im lặng",
        FanMode.Advanced => "Quạt tùy chỉnh",
        FanMode.CoolerBoost => "Cooler Boost",
        _ => mode.ToString(),
    };
}
