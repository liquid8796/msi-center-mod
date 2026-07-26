using System.Diagnostics;
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
/// Điểm khởi động: tự nâng quyền admin (UAC) → chặn chạy 2 bản → dựng DI container
/// → splash có tiến trình trong lúc khởi tạo phần cứng → mở cửa sổ chính.
/// (Base class System.Windows.Application do phần partial sinh từ App.xaml khai báo.)
/// </summary>
public partial class App
{
    private const string SingleInstanceMutexName = "MsiCenterMod_SingleInstance";

    /// <summary>Splash hiện tối thiểu bấy nhiêu ms để không nháy khi mọi thứ đã ấm sẵn.</summary>
    private const int MinimumSplashMs = 900;

    private Mutex? _singleInstanceMutex;
    private ServiceProvider? _services;
    private TrayIconService? _trayIcon;
    private MainWindow? _mainWindow;
    private SplashWindow? _splash;
    private AutoReapplyService? _autoReapply;

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

        // 2) Một bản duy nhất (bỏ qua ở chế độ dev --no-elevate để chạy song song bản test chỉ xem).
        bool isFirstInstance = true;
        if (!skipElevation)
        {
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out isFirstInstance);
        }

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

        // 3b) Ngôn ngữ UI: theo settings; --lang=xx để dev override không lưu.
        string? langOverride = e.Args
            .FirstOrDefault(a => a.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
            ?["--lang=".Length..];
        Loc.Initialize(langOverride ?? _services.GetRequiredService<ISettingsService>().Current.Language);

        // 4) --autostart (từ Task Scheduler): chạy ẩn dưới khay, không làm phiền bằng splash.
        bool startHidden = e.Args.Contains("--autostart", StringComparer.OrdinalIgnoreCase);
        if (!startHidden)
        {
            _splash = new SplashWindow();
            _splash.Show();
        }

        // 5) Phần còn lại chạy bất đồng bộ để splash vẽ được ngay.
        _ = InitializeAsync(e, startHidden);
    }

    /// <summary>
    /// Khởi tạo có báo tiến trình. Việc nặng (WMI, PerformanceCounter, nvidia-smi, schtasks)
    /// chạy trên thread nền — đó là lý do trước đây app "đứng hình" vài giây khi mở.
    /// ViewModel/Window bắt buộc dựng trên UI thread (DispatcherTimer, WPF).
    /// </summary>
    private async Task InitializeAsync(StartupEventArgs e, bool startHidden)
    {
        ServiceProvider services = _services!;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            ReportProgress(8, "S.Splash.Init");

            await Task.Run(() =>
            {
                ReportProgress(32, "S.Splash.Wmi");
                services.GetRequiredService<IHardwareController>();

                ReportProgress(64, "S.Splash.Sensors");
                services.GetRequiredService<ISystemMetricsService>();

                // Nạp cache trạng thái autostart (spawn schtasks.exe) ngay tại đây.
                ReportProgress(80, "S.Splash.Settings");
                services.GetRequiredService<IStartupService>().IsEnabled();
            }).ConfigureAwait(true);

            ReportProgress(92, "S.Splash.Ui");
            BuildUserInterface(e, startHidden);

            ReportProgress(100, "S.Splash.Ready");

            int remainingMs = MinimumSplashMs - (int)stopwatch.ElapsedMilliseconds;
            if (_splash is not null && remainingMs > 0)
            {
                await Task.Delay(remainingMs).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Khởi động thất bại", ex);
            CloseSplash();
            System.Windows.MessageBox.Show(
                Loc.Format("S.Splash.Failed", ex.Message, AppLogger.LogPath),
                "MSI Center Mod", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        CloseSplash();
        if (!startHidden)
        {
            _mainWindow?.Activate();
        }

        // Tự áp lại scenario cuối + lắng nghe resume/đổi nguồn AC-pin.
        _autoReapply = services.GetRequiredService<AutoReapplyService>();
        _autoReapply.Start(applyNow: true);

        AppLogger.Info($"Khởi động xong sau {stopwatch.ElapsedMilliseconds} ms.");
    }

    /// <summary>Dựng cửa sổ chính + tray icon (bắt buộc trên UI thread).</summary>
    private void BuildUserInterface(StartupEventArgs e, bool startHidden)
    {
        ServiceProvider services = _services!;
        _mainWindow = services.GetRequiredService<MainWindow>();

        // Dev: --tab=N mở thẳng tab thứ N (0-based); --tab-monitoring giữ tương thích cũ.
        if (e.Args.Contains("--tab-monitoring", StringComparer.OrdinalIgnoreCase))
        {
            services.GetRequiredService<MainViewModel>().SelectedTabIndex = 1;
        }
        else if (e.Args.FirstOrDefault(a => a.StartsWith("--tab=", StringComparison.OrdinalIgnoreCase)) is { } tabArg
                 && int.TryParse(tabArg["--tab=".Length..], out int tabIndex))
        {
            services.GetRequiredService<MainViewModel>().SelectedTabIndex = tabIndex;
        }

        _trayIcon = new TrayIconService(
            services.GetRequiredService<MainViewModel>(),
            services.GetRequiredService<ISettingsService>(),
            () => _mainWindow,
            ExitApplication);
        MainWindow = _mainWindow;

        if (!startHidden)
        {
            _mainWindow.Show();
        }
    }

    /// <summary>Cập nhật splash — gọi được từ cả UI thread lẫn thread nền.</summary>
    private void ReportProgress(double percent, string statusKey)
    {
        if (_splash is null)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            _splash.ViewModel.Report(percent, statusKey);
        }
        else
        {
            Dispatcher.BeginInvoke(() => _splash?.ViewModel.Report(percent, statusKey));
        }
    }

    private void CloseSplash()
    {
        _splash?.CloseWithFade();
        _splash = null;
    }

    private static ServiceProvider BuildServices(IElevationService elevation)
    {
        var services = new ServiceCollection();

        // Hạ tầng
        services.AddSingleton(elevation);
        services.AddSingleton<IMsiWmiClient, MsiWmiClient>();
        services.AddSingleton<IPowerOverlayService, PowerOverlayService>();
        services.AddSingleton<IScenarioRepository, JsonScenarioRepository>();
        services.AddSingleton<ISystemMetricsService, SystemMetricsService>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IStartupService, StartupTaskService>();
        services.AddSingleton<IBatteryChargeService, BatteryChargeService>();
        services.AddSingleton<IStorageInfoService, StorageInfoService>();
        services.AddSingleton<IMemoryCleaner, MemoryCleanerService>();
        services.AddSingleton<AutoReapplyService>();

        // Các aspect của scenario — thêm tính năng mới chỉ cần đăng ký thêm ở đây.
        services.AddSingleton<IScenarioAspect, PerformanceAspect>();
        services.AddSingleton<IScenarioAspect, FanAspect>();
        services.AddSingleton<IScenarioAspect, PowerOverlayAspect>();
        services.AddSingleton<IHardwareController, HardwareController>();

        // UI
        services.AddSingleton<MonitoringViewModel>();
        services.AddSingleton<DiagnosisViewModel>();
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
        _autoReapply?.Dispose();
        _trayIcon?.Dispose();
        _services?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
