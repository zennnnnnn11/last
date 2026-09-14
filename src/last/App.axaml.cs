using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using last.Core.Connection.Services;
using last.Services;
using last.ViewModels;
using last.Views;

namespace last;

public class App : Application
{
    private LcuConnectionCoordinator? _coordinator;
    private bool _isExplicitExit;
    private GameflowWindowLifecycleCoordinator? _lifecycleCoordinator;
    private MainWindowViewModel? _mainViewModel;
    private TrayIconManager? _trayManager;

    /// <summary>
    ///     指示当前是否正在显示退出确认模态弹窗。
    /// </summary>
    public static bool IsExitConfirmationShowing { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    ///     将主窗口缩小并隐藏至系统托盘。
    /// </summary>
    public static void HideMainWindowToTray()
    {
        if (Current is App app && app._lifecycleCoordinator is { } coordinator)
        {
            coordinator.HideToTray();
        }
        else if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWin })
        {
            mainWin.ShowInTaskbar = false;
            mainWin.Hide();
            MemoryTrimmer.TrimWorkingSetDeferred();
        }
    }

    /// <summary>
    ///     弹出 Linear 风格确认弹窗请求退出整个应用程序。
    /// </summary>
    public static async Task RequestExitAppAsync(Window? owner)
    {
        if (IsExitConfirmationShowing)
            return;

        IsExitConfirmationShowing = true;
        try
        {
            if (owner is not { IsVisible: true })
            {
                ShutdownExplicit();
                return;
            }

            var dialog = new ConfirmExitDialog();
            var confirmed = await dialog.ShowDialog<bool>(owner);
            if (confirmed) ShutdownExplicit();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Error displaying exit confirmation dialog", ex);
            // 异常时若无法弹出对话框，直接安全退出，避免程序假死卡住
            ShutdownExplicit();
        }
        finally
        {
            IsExitConfirmationShowing = false;
        }
    }

    /// <summary>
    ///     终结应用程序并退出进程。
    /// </summary>
    public static void ShutdownExplicit()
    {
        if (Current is App app) app._isExplicitExit = true;

        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
        else
            Environment.Exit(0);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // UI 线程未捕获异常全局兜底，防止局部绑定或事件错误导致主程序崩溃
        Dispatcher.UIThread.UnhandledException += (sender, e) =>
        {
            // 忽略任务取消异常（如切换标签或重新筛选引起的请求取消）
            if (e.Exception is OperationCanceledException or TaskCanceledException)
            {
                e.Handled = true;
                return;
            }

            AppLogger.Error("Unhandled UI thread exception caught by Dispatcher", e.Exception);
            e.Handled = true;
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 设为显式关闭模式：主窗口关闭时隐藏到托盘静默保活，仅通过托盘菜单退出时才终结进程
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _coordinator = new LcuConnectionCoordinator();
            _coordinator.Start();

            _mainViewModel = new MainWindowViewModel(_coordinator);
            var mainWindow = new MainWindow(_mainViewModel)
            {
                Icon = AppIconGenerator.GetOrCreateAppIcon()
            };

            // 拦截窗口关闭事件（如快捷键 Alt+F4）：弹出 Linear 退出确认弹窗
            mainWindow.Closing += (sender, e) =>
            {
                if (!_isExplicitExit)
                {
                    e.Cancel = true;
                    _ = RequestExitAppAsync(mainWindow);
                }
            };

            desktop.MainWindow = mainWindow;

            // 初始化托盘管理与右键菜单
            _trayManager = new TrayIconManager(
                () => _lifecycleCoordinator?.ShowAndActivate(),
                () => ShutdownExplicit(),
                () => _lifecycleCoordinator?.ToggleWindow()
            );

            // 初始化对局阶段与窗口生命周期自动化协调器（选人自弹置顶、进游戏休眠托盘、自动内存修剪）
            _lifecycleCoordinator = new GameflowWindowLifecycleCoordinator(
                mainWindow,
                _coordinator,
                _trayManager
            );

            // 注册单实例唤醒回调：多开时自动拉起托盘中的主窗口并置顶激活
            SingleInstanceManager.SetWakeUpAction(() => { _lifecycleCoordinator?.ShowAndActivate(); });

            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        SingleInstanceManager.SetWakeUpAction(null);

        _lifecycleCoordinator?.Dispose();
        _lifecycleCoordinator = null;

        _trayManager?.Dispose();
        _trayManager = null;

        _mainViewModel?.Dispose();
        _mainViewModel = null;

        _coordinator?.Dispose();
        _coordinator = null;
    }
}