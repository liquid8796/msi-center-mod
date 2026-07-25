using System.ComponentModel;
using System.Windows;
using MsiCenterMod.ViewModels;

namespace MsiCenterMod.Views;

public partial class MainWindow : Window
{
    /// <summary>true khi người dùng chọn Thoát từ tray — cho phép đóng thật.</summary>
    public bool AllowClose { get; set; }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>Đóng cửa sổ = thu xuống khay hệ thống (giống MSI Center), trừ khi Thoát thật.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
