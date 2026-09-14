using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using last.Core.Automation;
using last.Core.Connection.Models;
using last.Core.Connection.Services;
using last.Core.State.Models;
using last.Helpers;

namespace last.ViewModels;

public sealed partial class DeckCardViewModel : ObservableObject, IDisposable
{
    private readonly ILcuConnectionCoordinator _coordinator;
    private readonly MatchHistoryCardViewModel? _matchHistoryCard;
    private readonly CoalescingAction _syncCandidatesAction;
    private readonly CoalescingAction _syncEnemyTeamAction;
    private bool _disposed;
    private bool _isUpdatingFromBackend;

    public DeckCardViewModel(
        ILcuConnectionCoordinator coordinator,
        ProfileHeaderViewModel profile,
        MatchHistoryCardViewModel? matchHistoryCard = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _matchHistoryCard = matchHistoryCard;

        _syncCandidatesAction = new CoalescingAction(SyncCandidates, TimeSpan.FromMilliseconds(100));
        _syncEnemyTeamAction = new CoalescingAction(SyncEnemyTeamData, TimeSpan.FromMilliseconds(100));

        for (var i = 0; i < 10; i++)
            BenchSlots.Add(new BenchChampionSlotViewModel(_coordinator));

        for (var i = 0; i < 5; i++)
        {
            var row = new PlayerRowViewModel(_coordinator)
            {
                SelectionHandler = OnEnemyRowSelected
            };
            EnemyRows.Add(row);
        }

        _coordinator.Detector.StatusChanged += OnStatusChanged;
        _coordinator.Gameflow.PhaseChanged += OnPhaseChanged;
        _coordinator.Gameflow.SessionChanged += OnGameflowSessionChanged;
        _coordinator.AutoPlayAgain.SettingsChanged += OnAutoPlayAgainSettingsChanged;

        // 订阅选人联动事件（候补席、当前英雄、备选手牌池、选人会话）
        _coordinator.ChampSelect.BenchChampionsChanged += OnBenchChampionsChanged;
        _coordinator.ChampSelect.CurrentChampionChanged += OnCurrentChampionChanged;
        _coordinator.ChampSelect.SubsetChampionListChanged += OnSubsetChampionListChanged;
        _coordinator.ChampSelect.SessionChanged += OnSessionChanged;

        UpdateConnectionAndPhase();
        SyncFromBackendAutomation();
        SyncCandidates();
        SyncEnemyTeamData();

        CurrentPageIndex = _coordinator.Gameflow.Phase switch
        {
            GameflowPhase.ChampSelect => 1,
            GameflowPhase.GameStart or GameflowPhase.InProgress => 2,
            _ => 0
        };
    }

    public ProfileHeaderViewModel Profile { get; }

    public ObservableCollection<BenchChampionSlotViewModel> BenchSlots { get; } = [];

    public ObservableCollection<PlayerRowViewModel> EnemyRows { get; } = [];

    [ObservableProperty] public partial string PhaseText { get; set; } = "等待客户端";

    [ObservableProperty] public partial bool IsConnected { get; set; }

    [ObservableProperty] public partial int AutomationState { get; set; }

    [ObservableProperty] public partial int CurrentPageIndex { get; set; }

    [ObservableProperty] public partial int BenchCount { get; set; }

    [ObservableProperty] public partial string BenchCountText { get; set; } = "0/10";

    [ObservableProperty] public partial bool HasBenchChampions { get; set; }

    [ObservableProperty] public partial string EnemyHeaderText { get; set; } = "敌方阵容";

    [ObservableProperty] public partial string EnemyStatusText { get; set; } = "等待对局";

    [ObservableProperty] public partial bool HasEnemyPlayers { get; set; }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _coordinator.Detector.StatusChanged -= OnStatusChanged;
        _coordinator.Gameflow.PhaseChanged -= OnPhaseChanged;
        _coordinator.Gameflow.SessionChanged -= OnGameflowSessionChanged;
        _coordinator.AutoPlayAgain.SettingsChanged -= OnAutoPlayAgainSettingsChanged;

        _coordinator.ChampSelect.BenchChampionsChanged -= OnBenchChampionsChanged;
        _coordinator.ChampSelect.CurrentChampionChanged -= OnCurrentChampionChanged;
        _coordinator.ChampSelect.SubsetChampionListChanged -= OnSubsetChampionListChanged;
        _coordinator.ChampSelect.SessionChanged -= OnSessionChanged;

        _syncCandidatesAction.Dispose();
        _syncEnemyTeamAction.Dispose();

        Profile.Dispose();
    }

    [RelayCommand]
    private void ReturnToHome()
    {
        CurrentPageIndex = 0;
    }

    [RelayCommand]
    private void SelectPage(object? parameter)
    {
        var index = parameter switch
        {
            int i => i,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => 0
        };

        if (index >= 0 && index <= 2)
            CurrentPageIndex = index;
    }

    partial void OnAutomationStateChanged(int value)
    {
        if (_isUpdatingFromBackend)
            return;

        switch (value)
        {
            case 0:
                _coordinator.AutoAccept.IsEnabled = false;
                _coordinator.AutoPlayAgain.IsEnabled = false;
                break;
            case 1:
                _coordinator.AutoAccept.IsEnabled = true;
                _coordinator.AutoPlayAgain.IsEnabled = false;
                break;
            case 2:
                _coordinator.AutoAccept.IsEnabled = true;
                _coordinator.AutoPlayAgain.UpdateSettings(_coordinator.AutoPlayAgain.Settings with
                {
                    Enabled = true,
                    AutoSearchMatch = true
                });
                break;
        }
    }

    private void OnStatusChanged(ClientConnectionStatus status)
    {
        void Apply()
        {
            UpdateConnectionAndPhase();
            SyncEnemyTeamData();
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }

    private void OnPhaseChanged(GameflowPhase oldPhase, GameflowPhase newPhase)
    {
        void Apply()
        {
            UpdateConnectionAndPhase();
            CurrentPageIndex = newPhase switch
            {
                GameflowPhase.ChampSelect => 1,
                GameflowPhase.GameStart or GameflowPhase.InProgress => 2,
                _ => 0
            };
            SyncEnemyTeamData();
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }

    private void OnGameflowSessionChanged(GameflowSession? _)
    {
        DispatchSyncEnemyTeam();
    }

    private void OnAutoPlayAgainSettingsChanged(AutoPlayAgainSettings settings)
    {
        if (Dispatcher.UIThread.CheckAccess())
            SyncFromBackendAutomation();
        else
            Dispatcher.UIThread.Post(SyncFromBackendAutomation);
    }

    private void OnBenchChampionsChanged(IReadOnlyList<BenchChampion>? _)
    {
        DispatchSyncCandidates();
    }

    private void OnCurrentChampionChanged(int? _)
    {
        DispatchSyncCandidates();
    }

    private void OnSubsetChampionListChanged(IReadOnlyList<int>? _)
    {
        DispatchSyncCandidates();
    }

    private void OnSessionChanged(ChampSelectSession? _)
    {
        DispatchSyncCandidates();
        DispatchSyncEnemyTeam();
    }

    private void DispatchSyncCandidates()
    {
        _syncCandidatesAction.Invoke();
    }

    private void DispatchSyncEnemyTeam()
    {
        _syncEnemyTeamAction.Invoke();
    }

    /// <summary>
    ///     同步候选池英雄（手牌模式备选池 + 大乱斗常规板凳席，双源合并、排除自身、去重）
    /// </summary>
    private void SyncCandidates()
    {
        var candidates = new List<(int ChampionId, bool IsPriority)>();
        var seen = new HashSet<int>();

        var currentChamp = _coordinator.ChampSelect.CurrentChampion ?? 0;
        if (currentChamp > 0)
            seen.Add(currentChamp);

        // 源 A：手牌模式备选池
        var subset = _coordinator.ChampSelect.SubsetChampionList;
        if (subset is { Count: > 0 })
            foreach (var id in subset)
                if (id > 0 && seen.Add(id))
                {
                    candidates.Add((id, false));
                    if (candidates.Count >= 10) break;
                }

        // 源 B：大乱斗常规板凳席
        if (candidates.Count < 10)
        {
            var bench = _coordinator.ChampSelect.BenchChampions;
            if (bench is { Count: > 0 })
                foreach (var b in bench)
                    if (b.ChampionId > 0 && seen.Add(b.ChampionId))
                    {
                        candidates.Add((b.ChampionId, b.IsPriority));
                        if (candidates.Count >= 10) break;
                    }
        }

        var count = candidates.Count;
        BenchCount = count;
        BenchCountText = $"{count}/10";
        HasBenchChampions = count > 0;

        for (var i = 0; i < 10; i++)
            if (i < candidates.Count)
            {
                var (champId, isPriority) = candidates[i];
                BenchSlots[i].Update(champId, isPriority);
            }
            else
            {
                BenchSlots[i].Update(0, false);
            }
    }

    /// <summary>
    ///     同步敌方 5 人阵容数据
    /// </summary>
    public void SyncEnemyTeamData()
    {
        if (_coordinator.Detector.Status != ClientConnectionStatus.Connected)
        {
            EnemyStatusText = "未连接";
            HasEnemyPlayers = false;
            foreach (var row in EnemyRows)
                row.Reset();
            return;
        }

        var phase = _coordinator.Gameflow.Phase;
        var localPuuid = _coordinator.Summoner.Me?.Puuid ?? string.Empty;

        // 场景 1：选人阶段（部分模式可能显示敌方）
        if (phase == GameflowPhase.ChampSelect)
        {
            var champSession = _coordinator.ChampSelect.Session;
            if (champSession?.TheirTeam != null && champSession.TheirTeam.Count > 0)
            {
                var team = champSession.TheirTeam;
                var count = Math.Min(5, team.Count);
                HasEnemyPlayers = true;
                EnemyStatusText = $"{count} 人就绪";

                for (var i = 0; i < 5; i++)
                    if (i < team.Count)
                    {
                        var player = team[i];
                        var name = !string.IsNullOrWhiteSpace(player.GameName)
                            ? player.GameName
                            : !string.IsNullOrWhiteSpace(player.PlayerAlias)
                                ? player.PlayerAlias
                                : $"对手 {i + 1}";

                        EnemyRows[i].Update(
                            player.ChampionId,
                            (int)player.Spell1Id,
                            (int)player.Spell2Id,
                            name,
                            player.TagLine,
                            player.Puuid,
                            false,
                            true);
                    }
                    else
                    {
                        EnemyRows[i].Reset();
                    }

                return;
            }
        }

        // 场景 2：对局加载与对局进行中（GameStart / InProgress）
        if (phase is GameflowPhase.GameStart or GameflowPhase.InProgress)
        {
            var gameflowSession = _coordinator.Gameflow.Session;
            var gameData = gameflowSession?.GameData;

            if (gameData != null)
            {
                var isTeamOneAlly = true;
                if (!string.IsNullOrEmpty(localPuuid) && gameData.TeamTwo != null)
                    if (gameData.TeamTwo.Any(p =>
                            string.Equals(p.Puuid, localPuuid, StringComparison.OrdinalIgnoreCase)))
                        isTeamOneAlly = false;

                var enemyTeam = isTeamOneAlly ? gameData.TeamTwo : gameData.TeamOne;
                if (enemyTeam != null && enemyTeam.Count > 0)
                {
                    HasEnemyPlayers = true;
                    EnemyStatusText = phase == GameflowPhase.InProgress ? "对局进行中" : "加载中";

                    for (var i = 0; i < 5; i++)
                        if (i < enemyTeam.Count)
                        {
                            var player = enemyTeam[i];
                            var selection = gameData.PlayerChampionSelections?
                                .FirstOrDefault(s =>
                                    string.Equals(s.Puuid, player.Puuid, StringComparison.OrdinalIgnoreCase));

                            var champId = selection?.ChampionId > 0 ? selection.ChampionId : player.ChampionId;
                            var s1 = selection?.Spell1Id ?? 0;
                            var s2 = selection?.Spell2Id ?? 0;

                            // 优先读取现代 Riot ID (riotIdGameName / riotIdTagLine)，优雅兼容旧 summonerName / gameName / playerAlias
                            var name = !string.IsNullOrWhiteSpace(player.RiotIdGameName)
                                ? player.RiotIdGameName
                                : !string.IsNullOrWhiteSpace(player.GameName)
                                    ? player.GameName
                                    : !string.IsNullOrWhiteSpace(player.SummonerName)
                                        ? player.SummonerName
                                        : !string.IsNullOrWhiteSpace(player.PlayerAlias)
                                            ? player.PlayerAlias
                                            : !string.IsNullOrWhiteSpace(player.RiotId)
                                                ? player.RiotId.Contains('#')
                                                    ? player.RiotId.Split('#')[0]
                                                    : player.RiotId
                                                : $"对手 {i + 1}";

                            var tag = !string.IsNullOrWhiteSpace(player.RiotIdTagLine)
                                ? player.RiotIdTagLine
                                : player.TagLine;

                            // 若此前已有真实名称，坚决保留不被占位名冲掉
                            if (name.StartsWith("对手 ") &&
                                !string.IsNullOrWhiteSpace(EnemyRows[i].SummonerName) &&
                                !EnemyRows[i].SummonerName.StartsWith("对手 "))
                            {
                                name = EnemyRows[i].SummonerName;
                                if (string.IsNullOrWhiteSpace(tag))
                                    tag = EnemyRows[i].TagLine;
                            }

                            EnemyRows[i].Update(champId, s1, s2, name, tag, player.Puuid, false, true);
                        }
                        else
                        {
                            EnemyRows[i].Reset();
                        }

                    return;
                }
            }
        }

        // 场景 3：空闲状态
        EnemyStatusText = "等待对局";
        HasEnemyPlayers = false;
        foreach (var row in EnemyRows)
            row.Reset();
    }

    private void OnEnemyRowSelected(PlayerRowViewModel row)
    {
        if (_matchHistoryCard == null)
            return;

        if (row.IsSelected)
        {
            row.IsSelected = false;
            _matchHistoryCard.ResetToLocalPlayer();
        }
        else
        {
            foreach (var r in EnemyRows)
                r.IsSelected = false;

            row.IsSelected = true;
            _matchHistoryCard.InspectPlayer(
                row.Puuid,
                row.SummonerName,
                row.ChampionName,
                true,
                () => row.IsSelected = false);
        }
    }

    private void UpdateConnectionAndPhase()
    {
        var status = _coordinator.Detector.Status;
        var connected = status == ClientConnectionStatus.Connected;
        IsConnected = connected;

        if (!connected)
        {
            PhaseText = "等待客户端";
            return;
        }

        var phase = _coordinator.Gameflow.Phase;
        PhaseText = phase switch
        {
            GameflowPhase.None or GameflowPhase.Lobby => "已就绪",
            GameflowPhase.ChampSelect => "英雄选择中",
            GameflowPhase.GameStart or GameflowPhase.InProgress or GameflowPhase.Reconnect => "对局进行中",
            GameflowPhase.EndOfGame or GameflowPhase.PreEndOfGame or GameflowPhase.WaitingForStats => "结算中",
            _ => "准备中"
        };
    }

    private void SyncFromBackendAutomation()
    {
        _isUpdatingFromBackend = true;
        try
        {
            var accept = _coordinator.AutoAccept.IsEnabled;
            var playAgain = _coordinator.AutoPlayAgain.IsEnabled;

            if (playAgain)
                AutomationState = 2;
            else if (accept)
                AutomationState = 1;
            else
                AutomationState = 0;
        }
        finally
        {
            _isUpdatingFromBackend = false;
        }
    }
}