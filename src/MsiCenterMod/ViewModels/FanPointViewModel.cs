using CommunityToolkit.Mvvm.ComponentModel;
using MsiCenterMod.Models;

namespace MsiCenterMod.ViewModels;

/// <summary>Một điểm trên đường cong quạt: nhãn nhiệt độ (cố định bởi EC) + tốc độ chỉnh được.</summary>
public sealed partial class FanPointViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedLabel))]
    private int _speed;

    /// <summary>Nhiệt độ ngưỡng của điểm (đọc từ EC, chỉ hiển thị).</summary>
    public int Temperature { get; }

    public string TemperatureLabel => Temperature <= 0
        ? Services.System.Loc.Get("S.Label.Idle")
        : $"{Temperature}°C";

    public string SpeedLabel => $"{Speed}%";

    /// <summary>Làm mới nhãn sau khi đổi ngôn ngữ.</summary>
    public void RefreshLabels() => OnPropertyChanged(nameof(TemperatureLabel));

    public FanPointViewModel(int temperature, int speed)
    {
        Temperature = temperature;
        _speed = FanCurve.Clamp(speed);
    }

    partial void OnSpeedChanged(int value)
    {
        int clamped = FanCurve.Clamp(value);
        if (clamped != value)
        {
            Speed = clamped;
        }
    }
}
