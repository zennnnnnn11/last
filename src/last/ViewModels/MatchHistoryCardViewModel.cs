using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using last.Core.Connection.Models;
using last.Core.Connection.Services;
using last.Core.MatchHistory.Models;
using last.Core.State.Models;
using last.Helpers;
using last.Services;

namespace last.ViewModels;

/// <summary>
///     卡片 3（战绩列表卡）视图模型，管理战绩列表拉取、模式过滤、胜率统计与刷新。
/// </summary>
public sealed partial class MatchHistoryCardViewModel : ObservableObject, IDisposable
{
    private const int MaxLoadedMatches = 200;
    private readonly ILcuConnectionCoordinator _coordinator;
    private readonly Lock _loadGate = new();
    private readonly HashSet<long> _seenGameIds = [];
    private string _currentPuuid = string.Empty;
    private bool _disposed;
    private bool _isLoadingMoreSync;
    private long _lastRefreshTimestamp;
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _loadMoreCts;
    private string _localPuuid = string.Empty;
    private Action? _onUnselected;
    private int _serverOffset;

    public MatchHistoryCardViewModel(ILcuConnectionCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

        SelectedFilter = MatchModeFilter.All;

        _coordinator.Detector.StatusChanged += OnStatusChanged;
        _coordinator.Summoner.CurrentSummonerChanged += OnCurrentSummonerChanged;
        _coordinator.Gameflow.PhaseChanged += OnPhaseChanged;

        if (_coordinator.Detector.Status == ClientConnectionStatus.Connected &&
            _coordinator.Summoner.Me is not null)
        {
            _localPuuid = _coordinator.Summoner.Me.Puuid;
            _currentPuuid = _localPuuid;
            _ = LoadMatchesAsync();
        }
    }

    public BulkObservableCollection<MatchHistoryItemViewModel> Matches { get; } = [];

    public IReadOnlyList<MatchModeFilter> FilterOptions => MatchModeFilter.PresetFilters;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilterAllSelected))]
    [NotifyPropertyChangedFor(nameof(IsFilterRankedSoloSelected))]
    [NotifyPropertyChangedFor(nameof(IsFilterRankedFlexSelected))]
    [NotifyPropertyChangedFor(nameof(IsFilterNormalSelected))]
    [NotifyPropertyChangedFor(nameof(IsFilterAramSelected))]
    [NotifyPropertyChangedFor(nameof(IsFilterArenaSelected))]
    [NotifyPropertyChangedFor(nameof(IsFilterSpecialSelected))]
    public partial MatchModeFilter SelectedFilter { get; set; } = MatchModeFilter.All;

    public bool IsFilterAllSelected => SelectedFilter?.Key == "ALL";
    public bool IsFilterRankedSoloSelected => SelectedFilter?.Key == "RANKED_SOLO";
    public bool IsFilterRankedFlexSelected => SelectedFilter?.Key == "RANKED_FLEX";
    public bool IsFilterNormalSelected => SelectedFilter?.Key == "NORMAL";
    public bool IsFilterAramSelected => SelectedFilter?.Key == "ARAM";
    public bool IsFilterArenaSelected => SelectedFilter?.Key == "ARENA";
    public bool IsFilterSpecialSelected => SelectedFilter?.Key == "SPECIAL";

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial bool IsLoadingMore { get; set; }

    [ObservableProperty] public partial bool HasMoreMatches { get; set; } = true;

    [ObservableProperty] public partial bool HasMatches { get; set; }

    [ObservableProperty] public partial string SummaryStatText { get; set; } = "近20场 · 胜率 --%";

    [ObservableProperty] public partial string EmptyText { get; set; } = "等待连接游戏客户端";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInspectingAlly))]
    public partial bool IsInspectingOtherPlayer { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInspectingAlly))]
    public partial bool IsInspectingEnemy { get; set; }

    public bool IsInspectingAlly => IsInspectingOtherPlayer && !IsInspectingEnemy;

    [ObservableProperty] public partial string InspectedPlayerName { get; set; } = string.Empty;

    [ObservableProperty] public partial string HeaderTitleText { get; set; } = "近期战绩";

    public void Dispose()
    {
        lock (_loadGate)
        {
            if (_disposed)
                return;

            _disposed = true;
            try
            {
                _loadCts?.Cancel();
            }
            catch
            {
            }

            _loadCts?.Dispose();
            _loadCts = null;

            try
            {
                _loadMoreCts?.Cancel();
            }
            catch
            {
            }

            _loadMoreCts?.Dispose();
            _loadMoreCts = null;
        }

        _coordinator.Detector.StatusChanged -= OnStatusChanged;
        _coordinator.Summoner.CurrentSummonerChanged -= OnCurrentSummonerChanged;
        _coordinator.Gameflow.PhaseChanged -= OnPhaseChanged;
        RequestScrollToTop = null;

        ClearMatches();
    }

    /// <summary>
    ///     请求 UI 滚动视图重置滚至顶部（切换模式或玩家时通知 View）
    /// </summary>
    public event Action? RequestScrollToTop;

    public void InspectPlayer(
        string puuid,
        string summonerName,
        string? championName,
        bool isEnemy,
        Action? onUnselected = null)
    {
        if (string.IsNullOrWhiteSpace(puuid))
            return;

        // 如果传入的是本地玩家自己的 puuid，则直接视为切回自己
        if (!string.IsNullOrWhiteSpace(_localPuuid) &&
            string.Equals(puuid, _localPuuid, StringComparison.OrdinalIgnoreCase))
        {
            ResetToLocalPlayer();
            return;
        }

        // 触发旧选中的取消回调（让旧的选中行熄灭高亮）
        _onUnselected?.Invoke();
        _onUnselected = onUnselected;

        IsInspectingOtherPlayer = true;
        IsInspectingEnemy = isEnemy;
        InspectedPlayerName = !string.IsNullOrWhiteSpace(championName) ? championName : summonerName;
        HeaderTitleText = $"战绩 · {InspectedPlayerName}";

        _currentPuuid = puuid;
        _ = LoadMatchesAsync();
    }

    [RelayCommand]
    public void ResetToLocalPlayer()
    {
        _onUnselected?.Invoke();
        _onUnselected = null;

        IsInspectingOtherPlayer = false;
        IsInspectingEnemy = false;
        InspectedPlayerName = string.Empty;
        HeaderTitleText = "近期战绩";

        _currentPuuid = _localPuuid;
        _ = LoadMatchesAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var now = Environment.TickCount64;
        if (now - _lastRefreshTimestamp < 1200 || IsLoading)
            return;

        _lastRefreshTimestamp = now;
        await LoadMatchesAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    public void SelectFilter(MatchModeFilter? option)
    {
        if (option is not null && !Equals(SelectedFilter, option))
            SelectedFilter = option;
    }

    partial void OnSelectedFilterChanged(MatchModeFilter value)
    {
        _ = LoadMatchesAsync();
    }

    public async Task LoadMatchesAsync()
    {
        var status = _coordinator.Detector.Status;
        if (status != ClientConnectionStatus.Connected)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ClearMatches();
                HasMatches = false;
                EmptyText = "等待连接游戏客户端";
                SummaryStatText = "等待客户端";
                IsLoading = false;
            });
            return;
        }

        var puuid = !string.IsNullOrWhiteSpace(_currentPuuid) ? _currentPuuid : _localPuuid;
        if (string.IsNullOrWhiteSpace(puuid))
        {
            puuid = _coordinator.Summoner.Me?.Puuid ?? string.Empty;
            _localPuuid = puuid;
            _currentPuuid = puuid;
        }

        if (string.IsNullOrWhiteSpace(puuid))
        {
            Dispatcher.UIThread.Post(() =>
            {
                ClearMatches();
                HasMatches = false;
                EmptyText = "正在获取召唤师信息...";
                SummaryStatText = "正在识别账号";
                IsLoading = false;
            });
            return;
        }

        CancellationToken token;
        lock (_loadGate)
        {
            if (_disposed)
                return;

            try
            {
                _loadCts?.Cancel();
            }
            catch
            {
            }

            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            token = _loadCts.Token;

            try
            {
                _loadMoreCts?.Cancel();
            }
            catch
            {
            }

            _loadMoreCts?.Dispose();
            _loadMoreCts = null;
            _isLoadingMoreSync = false;
            _serverOffset = 0;
        }

        Dispatcher.UIThread.Post(() =>
        {
            IsLoading = true;
            IsLoadingMore = false;
            HasMoreMatches = true;
        });

        try
        {
            var summaries = await _coordinator.MatchHistory
                .GetMatchHistoryAsync(puuid, 0, 20, SelectedFilter, cancellationToken: token)
                .ConfigureAwait(false);

            if (token.IsCancellationRequested)
                return;

            var items = new List<MatchHistoryItemViewModel>(summaries.Count);
            foreach (var match in summaries)
            {
                if (token.IsCancellationRequested)
                    return;
                items.Add(new MatchHistoryItemViewModel(match, puuid, _coordinator));
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (token.IsCancellationRequested || _disposed)
                    return;

                ClearMatches();
                var toAdd = new List<MatchHistoryItemViewModel>(items.Count);
                foreach (var item in items)
                    if (_seenGameIds.Add(item.GameId))
                        toAdd.Add(item);

                Matches.AddRange(toAdd);

                _serverOffset = summaries.Count;
                HasMatches = Matches.Count > 0;
                IsLoading = false;
                HasMoreMatches = summaries.Count >= 20;

                RequestScrollToTop?.Invoke();

                if (HasMatches)
                {
                    var wins = Matches.Count(m => m.IsWin && !m.IsRemake);
                    var validGames = Matches.Count(m => !m.IsRemake);
                    var winRate = validGames > 0 ? (int)Math.Round((double)wins / validGames * 100) : 0;
                    SummaryStatText = $"近{Matches.Count}场 · 胜率 {winRate}%";
                }
                else
                {
                    EmptyText = IsInspectingOtherPlayer
                        ? $"{InspectedPlayerName} 暂无{SelectedFilter.DisplayName}对局记录"
                        : $"暂无{SelectedFilter.DisplayName}对局记录";
                    SummaryStatText = "近0场 · 暂无记录";
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsLoading = false;
                if (Matches.Count == 0)
                {
                    HasMatches = false;
                    EmptyText = "战绩拉取异常，点击刷新重试";
                }
            });
        }
    }

    /// <summary>
    ///     触底无限滚动加载更多战绩（符合 Avalonia 官方滚动加载推荐规范）
    /// </summary>
    public async Task LoadMoreAsync()
    {
        // 同步锁防并发自取消：多帧连续滚动时仅允许一个任务执行
        if (_disposed || IsLoading || _isLoadingMoreSync || !HasMoreMatches)
            return;

        var status = _coordinator.Detector.Status;
        if (status != ClientConnectionStatus.Connected)
            return;

        var puuid = !string.IsNullOrWhiteSpace(_currentPuuid) ? _currentPuuid : _localPuuid;
        if (string.IsNullOrWhiteSpace(puuid))
            return;

        _isLoadingMoreSync = true;
        IsLoadingMore = true;

        _loadMoreCts?.Cancel();
        _loadMoreCts?.Dispose();
        _loadMoreCts = new CancellationTokenSource();
        var token = _loadMoreCts.Token;

        try
        {
            var startIndex = _serverOffset;
            var summaries = await _coordinator.MatchHistory
                .GetMatchHistoryAsync(puuid, startIndex, 20, SelectedFilter, cancellationToken: token)
                .ConfigureAwait(false);

            if (token.IsCancellationRequested)
                return;

            if (summaries.Count == 0)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_disposed) return;
                    HasMoreMatches = false;
                });
                return;
            }

            _serverOffset += summaries.Count;

            var items = new List<MatchHistoryItemViewModel>(summaries.Count);
            foreach (var match in summaries)
            {
                if (token.IsCancellationRequested)
                    return;
                items.Add(new MatchHistoryItemViewModel(match, puuid, _coordinator));
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed) return;

                var addedCount = 0;
                var toAdd = new List<MatchHistoryItemViewModel>(items.Count);
                foreach (var item in items)
                {
                    if (Matches.Count + toAdd.Count >= MaxLoadedMatches)
                    {
                        HasMoreMatches = false;
                        break;
                    }

                    if (_seenGameIds.Add(item.GameId))
                    {
                        toAdd.Add(item);
                        addedCount++;
                    }
                }

                if (toAdd.Count > 0)
                    Matches.AddRange(toAdd);

                if (Matches.Count >= MaxLoadedMatches || summaries.Count < 20 ||
                    (addedCount == 0 && summaries.Count == 0))
                    HasMoreMatches = false;

                if (Matches.Count > 0)
                {
                    var wins = Matches.Count(m => m.IsWin && !m.IsRemake);
                    var validGames = Matches.Count(m => !m.IsRemake);
                    var winRate = validGames > 0 ? (int)Math.Round((double)wins / validGames * 100) : 0;
                    SummaryStatText = $"近{Matches.Count}场 · 胜率 {winRate}%";
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppLogger.Error("[MatchHistoryCardViewModel] LoadMoreAsync error", ex);
        }
        finally
        {
            _isLoadingMoreSync = false;
            Dispatcher.UIThread.Post(() => IsLoadingMore = false);
        }
    }

    private void ClearMatches()
    {
        _seenGameIds.Clear();
        foreach (var m in Matches)
            m.Dispose();
        Matches.Clear();
    }

    private void OnStatusChanged(ClientConnectionStatus status)
    {
        if (status == ClientConnectionStatus.Connected)
            _ = LoadMatchesAsync();
        else
            Dispatcher.UIThread.Post(() =>
            {
                ClearMatches();
                HasMatches = false;
                EmptyText = "等待连接游戏客户端";
                SummaryStatText = "客户端已断开";
            });
    }

    private void OnCurrentSummonerChanged(SummonerInfo? me)
    {
        if (me != null && !string.Equals(_localPuuid, me.Puuid, StringComparison.Ordinal))
        {
            _localPuuid = me.Puuid;
            if (!IsInspectingOtherPlayer)
            {
                _currentPuuid = me.Puuid;
                _ = LoadMatchesAsync();
            }
        }
    }

    private void OnPhaseChanged(GameflowPhase oldPhase, GameflowPhase newPhase)
    {
        // 当退出选人或对局回到大厅/匹配时，自动恢复查看自己的战绩
        if (newPhase is GameflowPhase.None or GameflowPhase.Lobby or GameflowPhase.Matchmaking)
            if (IsInspectingOtherPlayer)
            {
                Dispatcher.UIThread.Post(ResetToLocalPlayer);
                return;
            }

        // 当游戏结束回到大厅或结算时，自动刷新战绩列表
        if (newPhase is GameflowPhase.EndOfGame or GameflowPhase.WaitingForStats or GameflowPhase.Lobby &&
            oldPhase is GameflowPhase.InProgress or GameflowPhase.GameStart)
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2000).ConfigureAwait(false);
                    await LoadMatchesAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    AppLogger.Warn($"Failed to auto-refresh matches on phase change: {ex.Message}");
                }
            });
    }
}