using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MsiCenterMod.Services.Abstractions;
using MsiCenterMod.Services.Hardware;
using MsiCenterMod.Services.Hardware.Aspects;
using MsiCenterMod.Services.Storage;
using MsiCenterMod.Services.System;
using MsiCenterMod.ViewModels;
using MsiCenterMod.Views;

namespace MsiCenterMod;

/// <summary>
/// Điểm khởi động: tự nâng quyền admin (UAC) → chặn chạy 2 bản → dựng DI container → mở cửa sổ chính.
/// (Base class System.Windows.Application do phần partial sinh từ App.xaml khai báo.)
/// </summary>
public partial class App
{
    private const string SingleInstanceMutexName = "MsiCenterMod_SingleInstance";

    private Mutex? _singleInstanceMutex;
    private ServiceProvider? _services;
    private TrayIconService? _trayIcon;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterGlobalExceptionHandlers();
        AppLogger.Info($"Khởi động, args: [{string.Join(" ", e.Args)}]");

        var elevation = new ElevationService();

        // 1) Tự nâng quyền: MSI_ACPI WMI bắt buộc quyền admin.
        //    --no-elevate dành cho dev muốn xem UI không cần UAC.
        bool skipElevation = e.Args.Contains("--no-elevate", StringComparer.OrdinalIgnoreCase);
        if (!elevation.IsElevated && !skipElevation && elevation.TryRelaunchElevated())
        {
            Shutdown();
            return;
        }

        // 2) Một bản duy nhất.
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            System.Windows.MessageBox.Show(
                "MSI Center Mod đang chạy — kiểm tra biểu tượng ở khay hệ thống.",
                "MSI Center Mod", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // 3) DI container.
        _services = BuildServices(elevation);

        // 4) Cửa sổ chính + tray icon.
        _mainWindow = _services.GetRequiredService<MainWindow>();
        _trayIcon = new TrayIconService(
            _services.GetRequiredService<MainViewModel>(),
            () => _mainWindow,
            ExitApplication);
        MainWindow = _mainWindow;
        _mainWindow.Show();
    }

    private static ServiceProvider BuildServices(IElevationService elevation)
    {
        var services = new ServiceCollection();

        // Hạ tầng
        services.AddSingleton(elevation);
        services.AddSingleton<IMsiWmiClient, MsiWmiClient>();
        services.AddSingleton<IPowerOverlayService, PowerOverlayService>();
        services.AddSingleton<IScenarioRepository, JsonScenarioRepository>();

        // Các aspect của scenario — thêm tính năng mới chỉ cần đăng ký thêm ở đây.
        services.AddSingleton<IScenarioAspect, PerformanceAspect>();
        services.AddSingleton<IScenarioAspect, FanAspect>();
        services.AddSingleton<IScenarioAspect, PowerOverlayAspect>();
        services.AddSingleton<IHardwareController, HardwareController>();

        // UI
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Lưới an toàn cuối cùng: mọi exception chưa bắt đều được ghi log.
    /// Exception trên UI thread được xử lý (app sống tiếp) thay vì chết lặng lẽ.
    /// </summary>
    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            AppLogger.Error("Exception chưa bắt trên UI thread", args.Exception);
            System.Windows.MessageBox.Show(
                $"Có lỗi không mong muốn (đã ghi vào log):\n{args.Exception.Message}\n\nLog: {AppLogger.LogPath}",
                "MSI Center Mod", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true; // không cho app chết
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.Error("Exception chưa bắt trong background task", args.Exception);
            args.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLogger.Error("Exception nghiêm trọng (app sẽ thoát)", args.ExceptionObject as Exception);
    }

    private void ExitApplication()
    {
        if (_mainWindow is not null)
        {
            _mainWindow.AllowClose = true;
        }

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _services?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
