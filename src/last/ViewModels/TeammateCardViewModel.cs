using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using last.Core.Connection.Models;
using last.Core.Connection.Services;
using last.Core.State.Models;
using last.Helpers;

namespace last.ViewModels;

/// <summary>
///     卡片 2（己方阵容卡，154px）视图模型，管理己方 5 行阵容及空态视图。
/// </summary>
public sealed partial class TeammateCardViewModel : ObservableObject, IDisposable
{
    private readonly List<ChampSelectTeamPlayer> _cachedChampSelectTeam = [];
    private readonly ILcuConnectionCoordinator _coordinator;
    private readonly MatchHistoryCardViewModel? _matchHistoryCard;
    private readonly CoalescingAction _syncTeamAction;
    private bool _disposed;

    public TeammateCardViewModel(
        ILcuConnectionCoordinator coordinator,
        MatchHistoryCardViewModel? matchHistoryCard = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _matchHistoryCard = matchHistoryCard;
        _syncTeamAction = new CoalescingAction(SyncTeamData, TimeSpan.FromMilliseconds(100));

        for (var i = 0; i < 5; i++)
        {
            var row = new PlayerRowViewModel(_coordinator)
            {
                SelectionHandler = OnRowSelected
            };
            AllyRows.Add(row);
        }

        _coordinator.Detector.StatusChanged += OnStatusChanged;
        _coordinator.Gameflow.PhaseChanged += OnPhaseChanged;
        _coordinator.Gameflow.SessionChanged += OnGameflowSessionChanged;
        _coordinator.ChampSelect.SessionChanged += OnChampSelectSessionChanged;
        _coordinator.ChampSelect.CurrentChampionChanged += OnCurrentChampionChanged;

        SyncTeamData();
    }

    public ObservableCollection<PlayerRowViewModel> AllyRows { get; } = [];

    [ObservableProperty] public partial string HeaderTitle { get; set; } = "我方阵容";

    [ObservableProperty] public partial string StatusText { get; set; } = "等待对局";

    [ObservableProperty] public partial bool IsInMatchOrSelect { get; set; }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cachedChampSelectTeam.Clear();

        _coordinator.Detector.StatusChanged -= OnStatusChanged;
        _coordinator.Gameflow.PhaseChanged -= OnPhaseChanged;
        _coordinator.Gameflow.SessionChanged -= OnGameflowSessionChanged;
        _coordinator.ChampSelect.SessionChanged -= OnChampSelectSessionChanged;
        _coordinator.ChampSelect.CurrentChampionChanged -= OnCurrentChampionChanged;

        _syncTeamAction.Dispose();
    }

    private void OnRowSelected(PlayerRowViewModel row)
    {
        if (_matchHistoryCard == null)
            return;

        if (row.IsSelected)
        {
            // 再次点击已选中的队友：反选并恢复查看自己的战绩
            row.IsSelected = false;
            _matchHistoryCard.ResetToLocalPlayer();
        }
        else
        {
            // 取消己方所有其他行的选中态
            foreach (var r in AllyRows)
                r.IsSelected = false;

            row.IsSelected = true;
            _matchHistoryCard.InspectPlayer(
                row.Puuid,
                row.SummonerName,
                row.ChampionName,
                false,
                () => row.IsSelected = false);
        }
    }

    private void OnStatusChanged(ClientConnectionStatus status)
    {
        DispatchSync();
    }

    private void OnPhaseChanged(GameflowPhase oldPhase, GameflowPhase newPhase)
    {
        DispatchSync();
    }

    private void OnGameflowSessionChanged(GameflowSession? session)
    {
        DispatchSync();
    }

    private void OnChampSelectSessionChanged(ChampSelectSession? session)
    {
        DispatchSync();
    }

    private void OnCurrentChampionChanged(int? championId)
    {
        DispatchSync();
    }

    private void DispatchSync()
    {
        _syncTeamAction.Invoke();
    }

    public void SyncTeamData()
    {
        if (_coordinator.Detector.Status != ClientConnectionStatus.Connected)
        {
            StatusText = "未连接";
            IsInMatchOrSelect = false;
            foreach (var row in AllyRows)
                row.Reset();
            return;
        }

        var phase = _coordinator.Gameflow.Phase;
        var localPuuid = _coordinator.Summoner.Me?.Puuid ?? string.Empty;

        // 场景 1：英雄选择阶段（ChampSelect）
        if (phase == GameflowPhase.ChampSelect)
        {
            var champSession = _coordinator.ChampSelect.Session;
            if (champSession?.MyTeam != null && champSession.MyTeam.Count > 0)
            {
                var team = champSession.MyTeam;
                _cachedChampSelectTeam.Clear();
                _cachedChampSelectTeam.AddRange(team);

                var count = Math.Min(5, team.Count);
                IsInMatchOrSelect = true;
                StatusText = $"{count} 人就绪";

                for (var i = 0; i < 5; i++)
                    if (i < team.Count)
                    {
                        var player = team[i];
                        var isLocal = player.CellId == champSession.LocalPlayerCellId ||
                                      (!string.IsNullOrEmpty(localPuuid) && string.Equals(player.Puuid, localPuuid,
                                          StringComparison.OrdinalIgnoreCase));
                        var name = !string.IsNullOrWhiteSpace(player.GameName)
                            ? player.GameName
                            : !string.IsNullOrWhiteSpace(player.PlayerAlias)
                                ? player.PlayerAlias
                                : $"队友 {i + 1}";

                        AllyRows[i].Update(
                            player.ChampionId,
                            (int)player.Spell1Id,
                            (int)player.Spell2Id,
                            name,
                            player.TagLine,
                            player.Puuid,
                            isLocal);
                    }
                    else
                    {
                        AllyRows[i].Reset();
                    }

                return;
            }
        }

        // 场景 2：对局加载与游戏中（GameStart / InProgress）
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

                var allyTeam = isTeamOneAlly ? gameData.TeamOne : gameData.TeamTwo;
                if (allyTeam != null && allyTeam.Count > 0)
                {
                    IsInMatchOrSelect = true;
                    StatusText = phase == GameflowPhase.InProgress ? "对局进行中" : "加载中";

                    for (var i = 0; i < 5; i++)
                        if (i < allyTeam.Count)
                        {
                            var player = allyTeam[i];
                            var isLocal = !string.IsNullOrEmpty(localPuuid) &&
                                          string.Equals(player.Puuid, localPuuid, StringComparison.OrdinalIgnoreCase);

                            var selection = gameData.PlayerChampionSelections?
                                .FirstOrDefault(s =>
                                    !string.IsNullOrEmpty(s.Puuid) &&
                                    string.Equals(s.Puuid, player.Puuid, StringComparison.OrdinalIgnoreCase));

                            var champId = selection?.ChampionId > 0 ? selection.ChampionId : player.ChampionId;
                            var s1 = selection?.Spell1Id ?? 0;
                            var s2 = selection?.Spell2Id ?? 0;

                            // 容错：如果匿名导致在游戏数据中匹配不到英雄，回落到选人阶段缓存
                            if (champId <= 0 && i < _cachedChampSelectTeam.Count)
                            {
                                var fallbackPick = _cachedChampSelectTeam[i];
                                if (fallbackPick.ChampionId > 0)
                                {
                                    champId = fallbackPick.ChampionId;
                                    if (s1 <= 0) s1 = (int)fallbackPick.Spell1Id;
                                    if (s2 <= 0) s2 = (int)fallbackPick.Spell2Id;
                                }
                            }

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
                                                : $"队友 {i + 1}";

                            var tag = !string.IsNullOrWhiteSpace(player.RiotIdTagLine)
                                ? player.RiotIdTagLine
                                : player.TagLine;

                            // 若当前从对局接口拿到的是占位名，但此前选人阶段已有正常名称，坚决保留此前真实名称不被冲掉
                            if (name.StartsWith("队友 ") &&
                                !string.IsNullOrWhiteSpace(AllyRows[i].SummonerName) &&
                                !AllyRows[i].SummonerName.StartsWith("队友 "))
                            {
                                name = AllyRows[i].SummonerName;
                                if (string.IsNullOrWhiteSpace(tag))
                                    tag = AllyRows[i].TagLine;
                            }

                            AllyRows[i].Update(champId, s1, s2, name, tag, player.Puuid, isLocal);
                        }
                        else if (i < _cachedChampSelectTeam.Count && _cachedChampSelectTeam[i].ChampionId > 0)
                        {
                            // 容错：若对局初期 allyTeam 缺少该匿名玩家，从选人阶段缓存平滑继承英雄与技能
                            var fallbackPick = _cachedChampSelectTeam[i];
                            var isLocal = !string.IsNullOrEmpty(localPuuid) &&
                                          string.Equals(fallbackPick.Puuid, localPuuid,
                                              StringComparison.OrdinalIgnoreCase);
                            var name = !string.IsNullOrWhiteSpace(fallbackPick.GameName)
                                ? fallbackPick.GameName
                                : !string.IsNullOrWhiteSpace(fallbackPick.PlayerAlias)
                                    ? fallbackPick.PlayerAlias
                                    : !string.IsNullOrWhiteSpace(AllyRows[i].SummonerName) &&
                                      !AllyRows[i].SummonerName.StartsWith("队友 ")
                                        ? AllyRows[i].SummonerName
                                        : $"队友 {i + 1}";

                            var tag = !string.IsNullOrWhiteSpace(fallbackPick.TagLine)
                                ? fallbackPick.TagLine
                                : AllyRows[i].TagLine;

                            AllyRows[i].Update(
                                fallbackPick.ChampionId,
                                (int)fallbackPick.Spell1Id,
                                (int)fallbackPick.Spell2Id,
                                name,
                                tag,
                                fallbackPick.Puuid,
                                isLocal);
                        }
                        else
                        {
                            AllyRows[i].Reset();
                        }

                    return;
                }
            }
        }

        // 场景 3：大厅或等待对局空闲状态（干净优雅空态，不铺散乱线框）
        _cachedChampSelectTeam.Clear();
        IsInMatchOrSelect = false;
        StatusText = "等待对局";
        foreach (var row in AllyRows)
            row.Reset();
    }
}