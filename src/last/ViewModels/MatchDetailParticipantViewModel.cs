using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using last.Core.Connection.Services;
using last.Core.MatchHistory.Models;
using last.Services;

namespace last.ViewModels;

/// <summary>
///     对局详情弹窗单个参与者视图模型。
///     支撑战况概览、伤害承伤、经济装备三大标签页的完整数据呈现，
///     并全面接入 MatchScorer 核心算法评分与 MVP/SVP 标识。
/// </summary>
public sealed partial class MatchDetailParticipantViewModel : ObservableObject
{
    private readonly ILcuConnectionCoordinator _coordinator;

    public MatchDetailParticipantViewModel(
        UnifiedParticipant participant,
        string localPuuid,
        double score,
        bool isMvp,
        bool isSvp,
        int teamTotalKills,
        long teamTotalDamage,
        long maxDamageInGame,
        long maxDamageTakenInGame,
        string championName,
        ILcuConnectionCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        ArgumentNullException.ThrowIfNull(participant);

        Puuid = participant.Puuid;
        SummonerName = participant.SummonerName;
        TeamId = participant.TeamId;
        ChampionId = participant.ChampionId;
        ChampionName = !string.IsNullOrWhiteSpace(championName) ? championName : participant.ChampionName;
        IsWin = participant.IsWin;
        IsLocalPlayer = string.Equals(participant.Puuid, localPuuid, StringComparison.OrdinalIgnoreCase);

        // 核心评分模型绑定
        Score = score;
        ScoreText = score.ToString("F1", CultureInfo.InvariantCulture);
        IsMvp = isMvp;
        IsSvp = isSvp;

        // 标签页 1：战况概览基础数据
        Kills = participant.Kills;
        Deaths = participant.Deaths;
        Assists = participant.Assists;
        KdaRatioText = participant.Deaths == 0
            ? $"{participant.Kills + participant.Assists:0.0} KDA"
            : $"{(double)(participant.Kills + participant.Assists) / participant.Deaths:0.0} KDA";

        var kp = teamTotalKills > 0
            ? Math.Clamp((int)Math.Round((double)(participant.Kills + participant.Assists) / teamTotalKills * 100), 0,
                100)
            : 0;
        KpPercentageText = $"{kp}% 参战";

        // 标签页 2：伤害与承伤数据
        TotalDamageDealt = participant.TotalDamageDealtToChampions;
        DamageText = FormatMetric(participant.TotalDamageDealtToChampions);
        TotalDamageTaken = participant.TotalDamageTaken;
        DamageTakenText = FormatMetric(participant.TotalDamageTaken);

        DamageSharePercentage = teamTotalDamage > 0
            ? Math.Clamp((double)participant.TotalDamageDealtToChampions / teamTotalDamage * 100, 0, 100)
            : 0;
        DamageSharePercentageText = $"{DamageSharePercentage:F0}%";
        DamageShareFullText = $"输出占比 {DamageSharePercentage:F0}%";

        // 双柱图比例计算（基准最大宽度 140px）
        const double maxBarWidth = 140.0;
        DamageBarWidth = maxDamageInGame > 0
            ? Math.Max(4.0, (double)participant.TotalDamageDealtToChampions / maxDamageInGame * maxBarWidth)
            : 4.0;
        DamageTakenBarWidth = maxDamageTakenInGame > 0
            ? Math.Max(4.0, (double)participant.TotalDamageTaken / maxDamageTakenInGame * maxBarWidth)
            : 4.0;

        // 标签页 3：经济与符文数据
        GoldEarned = participant.GoldEarned;
        GoldText = FormatMetric(participant.GoldEarned);

        // 初始化装备与强化符文槽位
        InitializeSlots(participant);

        // 异步并行加载视觉资产
        _ = LoadAssetsAsync(participant);
    }

    public string Puuid { get; }
    public string SummonerName { get; }
    public int TeamId { get; }
    public int ChampionId { get; }
    public string ChampionName { get; }
    public bool IsWin { get; }
    public bool IsLocalPlayer { get; }

    // 评分与徽标
    public double Score { get; }
    public string ScoreText { get; }
    public bool IsMvp { get; }
    public bool IsSvp { get; }
    public bool HasMvpOrSvp => IsMvp || IsSvp;

    // KDA
    public int Kills { get; }
    public int Deaths { get; }
    public int Assists { get; }
    public string KdaRatioText { get; }
    public string KpPercentageText { get; }

    // 伤害与承伤
    public long TotalDamageDealt { get; }
    public string DamageText { get; }
    public long TotalDamageTaken { get; }
    public string DamageTakenText { get; }
    public double DamageBarWidth { get; }
    public double DamageTakenBarWidth { get; }
    public double DamageSharePercentage { get; }
    public string DamageSharePercentageText { get; }
    public string DamageShareFullText { get; }

    // 经济
    public int GoldEarned { get; }
    public string GoldText { get; }

    // 视觉资产
    [ObservableProperty] public partial Bitmap? ChampionAvatar { get; set; }
    [ObservableProperty] public partial Bitmap? Spell1Icon { get; set; }
    [ObservableProperty] public partial Bitmap? Spell2Icon { get; set; }

    // 槽位集合
    public ObservableCollection<MatchItemSlotViewModel> EquipmentSlots { get; } = [];
    public ObservableCollection<MatchAugmentSlotViewModel> AugmentSlots { get; } = [];
    public bool HasAugments => AugmentSlots.Count > 0;

    private void InitializeSlots(UnifiedParticipant participant)
    {
        // 7 个装备槽位（6 装备 + 1 饰品/特殊）
        var items = participant.Items ?? [];
        for (var i = 0; i < 7; i++)
        {
            var itemId = i < items.Count ? items[i] : 0;
            EquipmentSlots.Add(new MatchItemSlotViewModel
            {
                ItemId = itemId,
                HasItem = itemId > 0
            });
        }

        // 海克斯符文槽位（最多 5 个）
        var augments = participant.SafeAugments;
        var count = Math.Min(augments.Count, 5);
        for (var i = 0; i < count; i++)
        {
            var augId = augments[i];
            AugmentSlots.Add(new MatchAugmentSlotViewModel
            {
                AugmentId = augId,
                HasAugment = augId > 0
            });
        }
    }

    private async Task LoadAssetsAsync(UnifiedParticipant participant)
    {
        async Task LoadChampionAsync()
        {
            var bmp = await ChampionIconLoader.GetIconAsync(participant.ChampionId, _coordinator)
                .ConfigureAwait(false);
            if (bmp != null) Dispatcher.UIThread.Post(() => ChampionAvatar = bmp);
        }

        async Task LoadSpell1Async()
        {
            if (participant.Spell1Id > 0)
            {
                var bmp = await SummonerSpellIconLoader.GetIconAsync(participant.Spell1Id, _coordinator)
                    .ConfigureAwait(false);
                if (bmp != null) Dispatcher.UIThread.Post(() => Spell1Icon = bmp);
            }
        }

        async Task LoadSpell2Async()
        {
            if (participant.Spell2Id > 0)
            {
                var bmp = await SummonerSpellIconLoader.GetIconAsync(participant.Spell2Id, _coordinator)
                    .ConfigureAwait(false);
                if (bmp != null) Dispatcher.UIThread.Post(() => Spell2Icon = bmp);
            }
        }

        async Task LoadItemAsync(MatchItemSlotViewModel slot)
        {
            var bmp = await ItemIconLoader.GetIconAsync(slot.ItemId, _coordinator).ConfigureAwait(false);
            var info = _coordinator.ItemStaticData.GetItem(slot.ItemId);
            Dispatcher.UIThread.Post(() =>
            {
                slot.Icon = bmp;
                if (info != null) slot.ItemName = info.Name;
            });
        }

        async Task LoadAugmentAsync(MatchAugmentSlotViewModel slot)
        {
            var bmp = await AugmentIconLoader.GetIconAsync(slot.AugmentId, _coordinator).ConfigureAwait(false);
            var info = _coordinator.KiwiAugmentStaticData.GetAugmentInfo(slot.AugmentId);
            Dispatcher.UIThread.Post(() =>
            {
                slot.Icon = bmp;
                if (info != null)
                {
                    slot.Name = info.Name;
                    slot.Desc = info.Desc;
                    if (!string.IsNullOrWhiteSpace(info.BorderColorHex) &&
                        Color.TryParse(info.BorderColorHex, out var bc))
                        slot.BorderBrush = new SolidColorBrush(bc);
                    if (!string.IsNullOrWhiteSpace(info.BackgroundColorHex) &&
                        Color.TryParse(info.BackgroundColorHex, out var bgc))
                        slot.BackgroundBrush = new SolidColorBrush(bgc);
                }
            });
        }

        var loadTasks = new List<Task>
        {
            LoadChampionAsync(),
            LoadSpell1Async(),
            LoadSpell2Async()
        };

        // 异步加载装备图标
        for (var i = 0; i < EquipmentSlots.Count; i++)
        {
            var slot = EquipmentSlots[i];
            if (!slot.HasItem) continue;

            loadTasks.Add(LoadItemAsync(slot));
        }

        // 异步加载海克斯强化符文图标与边框色、底色
        if (!_coordinator.KiwiAugmentStaticData.IsInitialized)
            try
            {
                await _coordinator.KiwiAugmentStaticData.InitializeAsync().ConfigureAwait(false);
            }
            catch
            {
            }

        for (var i = 0; i < AugmentSlots.Count; i++)
        {
            var slot = AugmentSlots[i];
            if (!slot.HasAugment) continue;

            loadTasks.Add(LoadAugmentAsync(slot));
        }

        await Task.WhenAll(loadTasks).ConfigureAwait(false);
    }

    private static string FormatMetric(long val)
    {
        return val switch
        {
            >= 1_000_000 => $"{(double)val / 1_000_000:0.0}m",
            >= 1_000 => $"{(double)val / 1_000:0.0}k",
            _ => val.ToString(CultureInfo.InvariantCulture)
        };
    }
}