using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace last.Services;

/// <summary>
///     系统托盘图标与原生右键菜单协调管理器（Native AOT 安全，支持完整对称退订）。
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly NativeMenuItem _exitMenuItem;
    private readonly TrayIcons _iconsCollection;
    private readonly EventHandler _onExitClicked;
    private readonly EventHandler _onShowClicked;
    private readonly EventHandler _onTrayClicked;
    private readonly NativeMenuItem _showMenuItem;
    private readonly TrayIcon _trayIcon;
    private bool _disposed;

    public TrayIconManager(
        Action onOpenRequested,
        Action onExitRequested,
        Action onToggleRequested)
    {
        ArgumentNullException.ThrowIfNull(onOpenRequested);
        ArgumentNullException.ThrowIfNull(onExitRequested);
        ArgumentNullException.ThrowIfNull(onToggleRequested);

        _onShowClicked = (_, _) => onOpenRequested();
        _onExitClicked = (_, _) => onExitRequested();
        _onTrayClicked = (_, _) => onToggleRequested();

        _showMenuItem = new NativeMenuItem("打开主界面");
        _showMenuItem.Click += _onShowClicked;

        _exitMenuItem = new NativeMenuItem("退出程序");
        _exitMenuItem.Click += _onExitClicked;

        var menu = new NativeMenu
        {
            _showMenuItem,
            new NativeMenuItemSeparator(),
            _exitMenuItem
        };

        _trayIcon = new TrayIcon
        {
            Icon = AppIconGenerator.GetOrCreateAppIcon(),
            ToolTipText = "last - 英雄联盟助手",
            IsVisible = true,
            Menu = menu
        };

        _trayIcon.Clicked += _onTrayClicked;

        var currentApp = Application.Current;
        var existingIcons = currentApp is not null ? TrayIcon.GetIcons(currentApp) : null;
        if (existingIcons is null)
        {
            existingIcons = [];
            if (currentApp is not null)
                TrayIcon.SetIcons(currentApp, existingIcons);
        }

        _iconsCollection = existingIcons;
        _iconsCollection.Add(_trayIcon);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _trayIcon.Clicked -= _onTrayClicked;
        _showMenuItem.Click -= _onShowClicked;
        _exitMenuItem.Click -= _onExitClicked;

        _trayIcon.IsVisible = false;
        _iconsCollection.Remove(_trayIcon);
        _trayIcon.Dispose();
    }

    /// <summary>
    ///     更新托盘图标悬浮状态文本（UI 线程安全）。
    /// </summary>
    public void UpdateToolTip(string toolTip)
    {
        if (_disposed || string.IsNullOrWhiteSpace(toolTip))
            return;

        if (Dispatcher.UIThread.CheckAccess())
            _trayIcon.ToolTipText = toolTip;
        else
            Dispatcher.UIThread.Post(() =>
            {
                if (!_disposed)
                    _trayIcon.ToolTipText = toolTip;
            });
    }
}