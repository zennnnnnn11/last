using System;
using Avalonia.Controls;
using Avalonia.Threading;
using last.Core.Automation;
using last.Core.Connection.Models;
using last.Core.Connection.Services;
using last.Core.State.Models;

namespace last.Services;

/// <summary>
///     游戏阶段与主窗口生命周期自动化协调器（选人阶段自弹置顶、对局结束从托盘拉回并置顶、生命周期置顶复原）。
/// </summary>
public sealed class GameflowWindowLifecycleCoordinator : IDisposable
{
    private readonly ILcuConnectionCoordinator _coordinator;
    private readonly TrayIconManager _trayManager;
    private readonly Window _window;
    private bool _disposed;
    private bool _isAutoTopmost;

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
        _coordinator.Detector.DiagnosticUpdated += OnDiagnosticUpdated;
        _coordinator.AutoAccept.Accepted += OnAutoAccepted;
        _coordinator.BenchSwap.SwapExecuted += OnBenchSwapExecuted;
        _coordinator.AutoPlayAgain.Executed += OnAutoPlayAgainExecuted;

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
        _coordinator.Detector.DiagnosticUpdated -= OnDiagnosticUpdated;
        _coordinator.AutoAccept.Accepted -= OnAutoAccepted;
        _coordinator.BenchSwap.SwapExecuted -= OnBenchSwapExecuted;
        _coordinator.AutoPlayAgain.Executed -= OnAutoPlayAgainExecuted;
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
            RestoreTopmost();
        }
        else
        {
            var phase = _coordinator.StateCoordinator.Gameflow.Phase;
            UpdateStatusByPhase(phase);
        }
    }

    private void OnDiagnosticUpdated(ProcessScanDiagnosticInfo info)
    {
        if (_disposed || _coordinator.Detector.Status == ClientConnectionStatus.Connected)
            return;

        if (info.DetectedPids.Count > 1)
            _trayManager.UpdateToolTip("last - 检测到多个英雄联盟客户端正在运行");
        else if (info.ErrorMessage is { Length: > 0 } &&
                 info.ErrorMessage.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase))
            _trayManager.UpdateToolTip("last - 客户端访问权限不足，请尝试以管理员身份运行");
    }

    private void OnAutoAccepted(bool success)
    {
        if (_disposed || !success)
            return;

        _trayManager.UpdateToolTip("last - 已自动为您接受对局匹配");
    }

    private void OnBenchSwapExecuted(int championId, bool success)
    {
        if (_disposed || !success)
            return;

        var name = _coordinator.ChampionStaticData.GetChampionName(championId);
        var title = !string.IsNullOrWhiteSpace(name) ? name : $"英雄 {championId}";
        _trayManager.UpdateToolTip($"last - 极速抢选成功：已为您换取【{title}】");
    }

    private void OnAutoPlayAgainExecuted(AutoPlayAgainExecutionResult result)
    {
        if (_disposed || !result.Success)
            return;

        _trayManager.UpdateToolTip("last - 已自动为您重新返回房间并开启对局寻找");
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

            case GameflowPhase.GameStart:
            case GameflowPhase.InProgress:
                // 2. 进入游戏加载或对局中：自动收起至系统托盘并修剪内存
                HideToTray();
                break;

            case GameflowPhase.PreEndOfGame:
            case GameflowPhase.EndOfGame:
            case GameflowPhase.WaitingForStats:
                // 3. 对局结束结算阶段：重新从托盘中拉回桌面并置顶
                BringToFrontAndTopmost();
                break;

            case GameflowPhase.Lobby or GameflowPhase.None or GameflowPhase.Matchmaking:
                // 4. 离开选人或对局回到大厅/房间：若属于自动化临时置顶，自动取消置顶还原
                RestoreTopmost();

                // 若从游戏中直接跳回大厅或房间（如极速结算、重赛或投降直接退出），拉回桌面呈现战报
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
            GameflowPhase.GameStart => "last - 游戏加载中 (已休眠至托盘)",
            GameflowPhase.InProgress => "last - 游戏中 (已休眠至托盘)",
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

            if (!_window.ShowInTaskbar)
                _window.ShowInTaskbar = true;

            if (!_window.IsVisible)
                _window.Show();

            if (_window.WindowState == WindowState.Minimized)
                _window.WindowState = WindowState.Normal;

            _isAutoTopmost = true;
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

            if (!_window.ShowInTaskbar)
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
    ///     离开自动化关注阶段后，若当前置顶由自动化引发，则安全重置置顶状态。
    /// </summary>
    public void RestoreTopmost()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
                return;

            if (_isAutoTopmost)
            {
                _isAutoTopmost = false;
                _window.Topmost = false;
            }
        });
    }

    /// <summary>
    ///     隐藏主窗口至系统托盘并触发工作集修剪。
    /// </summary>
    public void HideToTray()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || App.IsExitConfirmationShowing)
                return;

            if (_window.ShowInTaskbar)
                _window.ShowInTaskbar = false;

            _window.Hide();
            MemoryTrimmer.TrimWorkingSetDeferred();
        });
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