using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using last.Core.Connection.Services;
using last.Core.MatchHistory.Models;
using last.Core.Scoring;

namespace last.ViewModels;

/// <summary>
///     对局战绩详情窗口 ViewModel。
///     深度整合 last.Core.Scoring.MatchScorer 核心算法，
///     1:1 复刻战况概览、伤害承伤、经济装备三大视图。
///     支持单局异步全量对局（10人全量数据）拉取，彻底解决列表仅返回单人摘要的问题。
/// </summary>
public sealed partial class MatchDetailViewModel : ObservableObject
{
    private readonly ILcuConnectionCoordinator _coordinator;
    private readonly string _localPuuid;

    public MatchDetailViewModel(
        UnifiedMatchSummary match,
        string localPuuid,
        ILcuConnectionCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _localPuuid = localPuuid;
        ArgumentNullException.ThrowIfNull(match);

        GameId = match.GameId;
        IsWin = match.IsWin;
        IsRemake = match.GameDuration > 0 && match.GameDuration < 240;

        DurationText = match.GameDuration > 0
            ? $"{match.GameDuration / 60:D2}:{match.GameDuration % 60:D2}"
            : "00:00";
        TimeAgoText = FormatTimeAgo(match.GameCreation);
        ResultBadgeText = IsRemake ? "重开" : IsWin ? "胜利" : "败北";

        // 初始化队伍对象
        AllyTeam = new MatchDetailTeamViewModel(100, true, IsWin, 0, 0, 0);
        EnemyTeam = new MatchDetailTeamViewModel(200, false, !IsWin, 0, 0, 0);

        // 应用传入的摘要对局数据
        ApplyMatchData(match, localPuuid);

        // 若列表摘要仅包含 1 位玩家（SGP 列表按需节约带宽机制），异步全量拉取本场完整 10 人详情
        if (match.Participants.Count < 10) _ = LoadFullMatchAsync(match.GameId, localPuuid);
    }

    public long GameId { get; }
    public bool IsWin { get; }
    public bool IsRemake { get; }
    public string DurationText { get; }
    public string TimeAgoText { get; }
    public string ResultBadgeText { get; }

    [ObservableProperty] public partial string QueueName { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsLoadingFullMatch { get; set; }

    // 比对条数据
    [ObservableProperty] public partial int BlueKills { get; set; }
    [ObservableProperty] public partial int RedKills { get; set; }
    [ObservableProperty] public partial double BlueSplitBarWidth { get; set; } = 70.0;
    [ObservableProperty] public partial double RedSplitBarWidth { get; set; } = 70.0;
    [ObservableProperty] public partial string AdvantageText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBlueAdvantage { get; set; }
    [ObservableProperty] public partial bool IsRedAdvantage { get; set; }

    // 两支队伍
    public MatchDetailTeamViewModel AllyTeam { get; }
    public MatchDetailTeamViewModel EnemyTeam { get; }

    // 选项卡切换 (0: 战况概览, 1: 伤害承伤, 2: 经济装备)
    [ObservableProperty] public partial int SelectedTabIndex { get; set; } = 0;

    public bool IsOverviewTab => SelectedTabIndex == 0;
    public bool IsDamageTab => SelectedTabIndex == 1;
    public bool IsEconomyTab => SelectedTabIndex == 2;

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsOverviewTab));
        OnPropertyChanged(nameof(IsDamageTab));
        OnPropertyChanged(nameof(IsEconomyTab));
    }

    [RelayCommand]
    public void SelectTab(string? indexStr)
    {
        if (int.TryParse(indexStr, out var idx) && idx >= 0 && idx <= 2) SelectedTabIndex = idx;
    }

    private void ApplyMatchData(UnifiedMatchSummary match, string localPuuid)
    {
        // 模式名称判定（是否包含海克斯强化）
        var hasAugments = match.Participants.Any(p => (p.Augments?.Count ?? 0) > 0);
        QueueName = ResolveQueueName(match.QueueId, match.GameMode, hasAugments);

        // 核心评分模型运行
        var champStatic = _coordinator.ChampionStaticData;
        var champRolesMap = champStatic.GetRolesMap();
        var (mvpPuuid, svpPuuid, scores) = MatchScorer.EvaluateScores(
            match.Participants,
            match.QueueId,
            match.GameMode,
            id => champRolesMap.GetValueOrDefault(id),
            id => _coordinator.ItemStaticData.GetItem(id)?.Categories,
            match.GameDuration
        );

        // 划分友方队伍与敌方队伍（若 PUUID 匹配未中，使用英雄 ID 兜底匹配本场玩家）
        var localParticipant = match.Participants.FirstOrDefault(p =>
                                   string.Equals(p.Puuid, localPuuid, StringComparison.OrdinalIgnoreCase))
                               ?? match.Participants.FirstOrDefault(p => p.ChampionId == match.ChampionId);
        var allyTeamId = localParticipant?.TeamId ?? 100;
        var enemyTeamId = allyTeamId == 100 ? 200 : 100;

        var allyList = match.Participants.Where(p => p.TeamId == allyTeamId).ToList();
        var enemyList = match.Participants.Where(p => p.TeamId == enemyTeamId).ToList();
        if (allyList.Count == 0 && enemyList.Count == 0)
        {
            allyList = match.Participants.Take(match.Participants.Count / 2).ToList();
            enemyList = match.Participants.Skip(match.Participants.Count / 2).ToList();
        }

        // 全局最高伤害与承伤（用于比例进度条）
        var maxDmg = Math.Max(1L, match.Participants.Max(p => p.TotalDamageDealtToChampions));
        var maxTank = Math.Max(1L, match.Participants.Max(p => p.TotalDamageTaken));

        // 队伍聚合指标
        var allyKills = allyList.Sum(p => p.Kills);
        var enemyKills = enemyList.Sum(p => p.Kills);
        var allyGold = allyList.Sum(p => (long)p.GoldEarned);
        var enemyGold = enemyList.Sum(p => (long)p.GoldEarned);
        var allyDmg = allyList.Sum(p => p.TotalDamageDealtToChampions);
        var enemyDmg = enemyList.Sum(p => p.TotalDamageDealtToChampions);

        var isAllyWin = allyList.FirstOrDefault()?.IsWin ?? match.IsWin;
        var isEnemyWin = !isAllyWin;

        // 蓝红击杀比对栏计算
        var blueKills = allyTeamId == 100 ? allyKills : enemyKills;
        var redKills = allyTeamId == 100 ? enemyKills : allyKills;
        BlueKills = blueKills;
        RedKills = redKills;

        var totalKills = Math.Max(1, blueKills + redKills);
        BlueSplitBarWidth = Math.Clamp(Math.Round(140.0 * blueKills / totalKills), 10.0, 130.0);
        RedSplitBarWidth = 140.0 - BlueSplitBarWidth;

        var blueGold = allyTeamId == 100 ? allyGold : enemyGold;
        var redGold = allyTeamId == 100 ? enemyGold : allyGold;
        var goldDiff = blueGold - redGold;

        if (goldDiff > 200)
        {
            AdvantageText = $"蓝方领先 +{goldDiff / 1000.0:0.0}k";
            IsBlueAdvantage = true;
            IsRedAdvantage = false;
        }
        else if (goldDiff < -200)
        {
            AdvantageText = $"红方领先 +{-goldDiff / 1000.0:0.0}k";
            IsBlueAdvantage = false;
            IsRedAdvantage = true;
        }
        else
        {
            AdvantageText = "经济持平";
            IsBlueAdvantage = false;
            IsRedAdvantage = false;
        }

        if (match.Participants.Count < 10)
            // 列表摘要仅含单人数据，等待 LoadFullMatchAsync 异步拉取 10 人完整战况后再填充队伍成员，避免 1 人闪变为 10 人
            return;

        // 更新队伍聚合统计
        AllyTeam.UpdateStats(allyKills, allyGold, allyDmg, isAllyWin);
        EnemyTeam.UpdateStats(enemyKills, enemyGold, enemyDmg, isEnemyWin);

        // 填充参与者列表
        AllyTeam.Participants.Clear();
        foreach (var p in allyList)
        {
            var sc = scores.GetValueOrDefault(p.Puuid, 50.0);
            var isMvp = string.Equals(p.Puuid, mvpPuuid, StringComparison.OrdinalIgnoreCase);
            var isSvp = string.Equals(p.Puuid, svpPuuid, StringComparison.OrdinalIgnoreCase);
            var cName = _coordinator.GameData.GetChampionName(p.ChampionId);

            AllyTeam.Participants.Add(new MatchDetailParticipantViewModel(
                p, localPuuid, sc, isMvp, isSvp, allyKills, allyDmg, maxDmg, maxTank, cName, _coordinator));
        }

        EnemyTeam.Participants.Clear();
        foreach (var p in enemyList)
        {
            var sc = scores.GetValueOrDefault(p.Puuid, 50.0);
            var isMvp = string.Equals(p.Puuid, mvpPuuid, StringComparison.OrdinalIgnoreCase);
            var isSvp = string.Equals(p.Puuid, svpPuuid, StringComparison.OrdinalIgnoreCase);
            var cName = _coordinator.GameData.GetChampionName(p.ChampionId);

            EnemyTeam.Participants.Add(new MatchDetailParticipantViewModel(
                p, localPuuid, sc, isMvp, isSvp, enemyKills, enemyDmg, maxDmg, maxTank, cName, _coordinator));
        }
    }

    private async Task LoadFullMatchAsync(long gameId, string localPuuid)
    {
        IsLoadingFullMatch = true;
        try
        {
            var fullMatch = await _coordinator.MatchHistory.GetGameSummaryAsync(gameId, localPuuid)
                .ConfigureAwait(false);
            if (fullMatch != null && fullMatch.Participants.Count > 1)
                await Dispatcher.UIThread.InvokeAsync(() => { ApplyMatchData(fullMatch, localPuuid); });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MatchDetailViewModel] Failed to load full match: {ex.Message}");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => { IsLoadingFullMatch = false; });
        }
    }

    private static string ResolveQueueName(int queueId, string gameMode, bool hasAugments)
    {
        return queueId switch
        {
            420 => "单双排位",
            440 => "灵活排位",
            430 => "匹配模式",
            490 => "快速匹配",
            450 or 2400 => hasAugments ? "海克斯大乱斗" : "极地大乱斗",
            1700 or 1710 => "斗魂竞技场",
            _ => !string.IsNullOrWhiteSpace(gameMode)
                ? gameMode.Equals("ARAM", StringComparison.OrdinalIgnoreCase)
                    ? hasAugments ? "海克斯大乱斗" : "极地大乱斗"
                    : gameMode.Equals("CLASSIC", StringComparison.OrdinalIgnoreCase)
                        ? "经典模式"
                        : gameMode
                : "对局详情"
        };
    }

    private static string FormatTimeAgo(long gameCreation)
    {
        if (gameCreation <= 0) return "近期";

        var createdUtc = gameCreation > 10_000_000_000
            ? DateTimeOffset.FromUnixTimeMilliseconds(gameCreation)
            : DateTimeOffset.FromUnixTimeSeconds(gameCreation);

        var diff = DateTimeOffset.UtcNow - createdUtc;
        if (diff.TotalMinutes < 1) return "刚刚";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}分钟前";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}小时前";
        if (diff.TotalDays < 30) return $"{(int)diff.TotalDays}天前";
        return createdUtc.ToLocalTime().ToString("MM-dd", CultureInfo.InvariantCulture);
    }
}