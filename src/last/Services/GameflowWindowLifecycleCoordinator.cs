using System;
using Avalonia.Controls;
using Avalonia.Threading;
using last.Core.Connection.Models;
using last.Core.Connection.Services;
using last.Core.State.Models;

namespace last.Services;

/// <summary>
///     游戏阶段与主窗口生命周期自动化协调器（选人阶段自弹置顶、对局结束从托盘拉回并置顶）。
/// </summary>
public sealed class GameflowWindowLifecycleCoordinator : IDisposable
{
    private readonly ILcuConnectionCoordinator _coordinator;
    private readonly TrayIconManager _trayManager;
    private readonly Window _window;
    private bool _disposed;

    public GameflowWindowLifecycleCoordinator(
        Window window,
        ILcuConnectionCoordinator coordinator,
        TrayIconManager trayManager)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _trayManager = trayManager ?? throw new ArgumentNullException(nameof(trayManager));

        _coordinator.StateCoordinator.Gameflow.PhaseChanged += OnGameflowPhaseChanged;
        _coordinator.Detector.StatusChanged += OnDetectorStatusChanged;

        // 初始化托盘文本
        UpdateInitialStatus();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _coordinator.StateCoordinator.Gameflow.PhaseChanged -= OnGameflowPhaseChanged;
        _coordinator.Detector.StatusChanged -= OnDetectorStatusChanged;
    }

    private void UpdateInitialStatus()
    {
        if (_coordinator.Detector.Status == ClientConnectionStatus.Connected)
        {
            var phase = _coordinator.StateCoordinator.Gameflow.Phase;
            UpdateStatusByPhase(phase);
        }
        else
        {
            _trayManager.UpdateToolTip("last - 等待英雄联盟客户端启动");
        }
    }

    private void OnDetectorStatusChanged(ClientConnectionStatus status)
    {
        if (_disposed)
            return;

        if (status != ClientConnectionStatus.Connected)
        {
            _trayManager.UpdateToolTip("last - 等待英雄联盟客户端启动");
        }
        else
        {
            var phase = _coordinator.StateCoordinator.Gameflow.Phase;
            UpdateStatusByPhase(phase);
        }
    }

    private void OnGameflowPhaseChanged(GameflowPhase oldPhase, GameflowPhase newPhase)
    {
        if (_disposed)
            return;

        UpdateStatusByPhase(newPhase);

        switch (newPhase)
        {
            case GameflowPhase.ChampSelect:
                // 1. 进入选人阶段：窗口从托盘自动拉回桌面最前并置顶
                BringToFrontAndTopmost();
                break;

            case GameflowPhase.PreEndOfGame:
            case GameflowPhase.EndOfGame:
            case GameflowPhase.WaitingForStats:
                // 2. 对局结束结算阶段：重新从托盘中拉回桌面并置顶
                BringToFrontAndTopmost();
                break;

            case GameflowPhase.Lobby or GameflowPhase.None:
                // 若从游戏中直接跳回大厅或房间（如极速结算、重赛或投降直接退出），同样拉回并置顶
                if (oldPhase is GameflowPhase.InProgress or GameflowPhase.GameStart or GameflowPhase.Reconnect)
                    BringToFrontAndTopmost();
                break;
        }
    }

    private void UpdateStatusByPhase(GameflowPhase phase)
    {
        var text = phase switch
        {
            GameflowPhase.ChampSelect => "last - 英雄选择中 (已置顶)",
            GameflowPhase.GameStart => "last - 游戏加载中",
            GameflowPhase.InProgress => "last - 游戏中",
            GameflowPhase.ReadyCheck => "last - 对局寻找完毕 (请接受)",
            GameflowPhase.Matchmaking => "last - 正在寻找对局中...",
            GameflowPhase.Lobby => "last - 房间大厅中",
            GameflowPhase.PreEndOfGame => "last - 对局即将结算 (已置顶)",
            GameflowPhase.EndOfGame => "last - 对局结算中 (已置顶)",
            GameflowPhase.Reconnect => "last - 正在重新连接游戏",
            GameflowPhase.WaitingForStats => "last - 等待数据统计 (已置顶)",
            _ => "last - 客户端已连接"
        };

        _trayManager.UpdateToolTip(text);
    }

    /// <summary>
    ///     将窗口从托盘唤起至桌面最前并强制置顶。
    /// </summary>
    public void BringToFrontAndTopmost()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
                return;

            _window.ShowInTaskbar = true;
            if (!_window.IsVisible)
                _window.Show();

            if (_window.WindowState == WindowState.Minimized)
                _window.WindowState = WindowState.Normal;

            _window.Topmost = true;
            _window.Activate();
            _window.Focus();
        });
    }

    /// <summary>
    ///     正常显示并激活窗口（非强制置顶，若之前置顶则保持）。
    /// </summary>
    public void ShowAndActivate()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
                return;

            _window.ShowInTaskbar = true;
            if (!_window.IsVisible)
                _window.Show();

            if (_window.WindowState == WindowState.Minimized)
                _window.WindowState = WindowState.Normal;

            _window.Activate();
            _window.Focus();
        });
    }

    /// <summary>
    ///     隐藏主窗口至系统托盘。
    /// </summary>
    public void HideToTray()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || App.IsExitConfirmationShowing)
                return;

            _window.ShowInTaskbar = false;
            _window.Hide();
            MemoryTrimmer.TrimWorkingSetDeferred();
        });
    }

    /// <summary>
    ///     隐藏主窗口至系统托盘。
    /// </summary>
    public void HideToTrayAndTrimMemory()
    {
        HideToTray();
    }

    /// <summary>
    ///     切换窗口显示/隐藏状态（供托盘图标单击交互使用）。
    /// </summary>
    public void ToggleWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
                return;

            if (_window.IsVisible)
                HideToTray();
            else
                ShowAndActivate();
        });
    }
}