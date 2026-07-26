using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using MsiCenterMod.ViewModels;

namespace MsiCenterMod.Views;

/// <summary>
/// Màn hình khởi động: hiện ngay khi app chạy, cập nhật tiến trình theo từng bước khởi tạo thật.
/// Thanh tiến trình được animate trong View (thuần chuyện hiển thị) thay vì bind trực tiếp,
/// để giá trị nhảy bậc trông vẫn mượt.
/// </summary>
public partial class SplashWindow : Window
{
    private const int ProgressAnimationMs = 260;
    private const int FadeOutMs = 200;

    public SplashViewModel ViewModel { get; } = new();

    public SplashWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SplashViewModel.Progress))
        {
            return;
        }

        var animation = new DoubleAnimation(ViewModel.Progress, TimeSpan.FromMilliseconds(ProgressAnimationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Bar.BeginAnimation(RangeBase.ValueProperty, animation);
    }

    /// <summary>Mờ dần rồi đóng — tránh giật khi cửa sổ chính hiện lên.</summary>
    public void CloseWithFade()
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(FadeOutMs));
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }
}
