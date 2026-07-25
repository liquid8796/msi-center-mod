using System.Drawing;
using System.IO;
using System.Windows;
using MsiCenterMod.Services.Abstractions;
using MsiCenterMod.ViewModels;
using WinForms = System.Windows.Forms;

namespace MsiCenterMod.Services.System;

/// <summary>
/// Icon khay hệ thống: chuột phải để áp nhanh scenario (scenario đang áp có dấu tick),
/// mở cửa sổ hoặc thoát. Menu dựng lại mỗi lần mở để khớp danh sách + ngôn ngữ hiện tại.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settings;
    private readonly Func<Views.MainWindow?> _windowAccessor;
    private readonly Action _exit;

    public TrayIconService(
        MainViewModel viewModel,
        ISettingsService settings,
        Func<Views.MainWindow?> windowAccessor,
        Action exit)
    {
        _viewModel = viewModel;
        _settings = settings;
        _windowAccessor = windowAccessor;
        _exit = exit;

        _notifyIcon = new WinForms.NotifyIcon
        {
            Text = "MSI Center Mod",
            Icon = LoadIcon(),
            Visible = true,
            ContextMenuStrip = new WinForms.ContextMenuStrip(),
        };
        _notifyIcon.ContextMenuStrip.Opening += (_, _) => RebuildMenu();
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();
    }

    private static Icon LoadIcon()
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                return Icon.ExtractAssociatedIcon(exePath) ?? SystemIcons.Application;
            }
        }
        catch
        {
            // rơi xuống icon mặc định
        }

        return SystemIcons.Application;
    }

    private void RebuildMenu()
    {
        WinForms.ContextMenuStrip menu = _notifyIcon.ContextMenuStrip!;
        menu.Items.Clear();

        var header = new WinForms.ToolStripMenuItem(Loc.Get("S.Tray.Header")) { Enabled = false };
        menu.Items.Add(header);

        Guid? lastAppliedId = _settings.Current.LastAppliedScenarioId;
        foreach (ScenarioViewModel scenario in _viewModel.Scenarios)
        {
            var item = new WinForms.ToolStripMenuItem($"{scenario.Glyph}  {scenario.Name}")
            {
                // Dấu tick ở scenario được áp gần nhất
                Checked = scenario.Profile.Id == lastAppliedId,
            };
            ScenarioViewModel captured = scenario;
            item.Click += async (_, _) =>
                await global::System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    () => _viewModel.ApplyScenarioAsync(captured));
            menu.Items.Add(item);
        }

        menu.Items.Add(new WinForms.ToolStripSeparator());

        var open = new WinForms.ToolStripMenuItem(Loc.Get("S.Tray.Open"));
        open.Click += (_, _) => ShowWindow();
        menu.Items.Add(open);

        var exit = new WinForms.ToolStripMenuItem(Loc.Get("S.Tray.Exit"));
        exit.Click += (_, _) => _exit();
        menu.Items.Add(exit);
    }

    private void ShowWindow()
    {
        Views.MainWindow? window = _windowAccessor();
        if (window is null)
        {
            return;
        }

        window.Show();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
