using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using last.Core.Connection.Services;
using last.Core.MatchHistory.Models;
using last.Services;
using last.Views;

namespace last.ViewModels;

/// <summary>
///     单个装备槽位视图模型（17×17px）
/// </summary>
public sealed partial class MatchItemSlotViewModel : ObservableObject
{
    [ObservableProperty] public partial int ItemId { get; set; }
    [ObservableProperty] public partial Bitmap? Icon { get; set; }
    [ObservableProperty] public partial bool HasItem { get; set; }
    [ObservableProperty] public partial string ItemName { get; set; } = string.Empty;
}

/// <summary>
///     单个海克斯强化符文槽位视图模型（17×17px）
/// </summary>
public sealed partial class MatchAugmentSlotViewModel : ObservableObject
{
    [ObservableProperty] public partial int AugmentId { get; set; }
    [ObservableProperty] public partial Bitmap? Icon { get; set; }
    [ObservableProperty] public partial bool HasAugment { get; set; }
    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    [ObservableProperty] public partial string Desc { get; set; } = string.Empty;
    [ObservableProperty] public partial IBrush BorderBrush { get; set; } = Brushes.Transparent;
    [ObservableProperty] public partial IBrush BackgroundBrush { get; set; } = Brushes.Transparent;
}

/// <summary>
///     单场战绩条目视图模型（Linear 52px 固定行高黄金比例）
/// </summary>
public sealed partial class MatchHistoryItemViewModel : ObservableObject
{
    private static readonly ConcurrentDictionary<long, MatchDetailWindow> _openWindows = new();
    private readonly ILcuConnectionCoordinator _coordinator;
    private readonly CancellationTokenSource _cts = new();
    private readonly string _localPuuid;

    public MatchHistoryItemViewModel(
        UnifiedMatchSummary match,
        string localPuuid,
        ILcuConnectionCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        ArgumentNullException.ThrowIfNull(match);

        MatchSummary = match;
        _localPuuid = localPuuid;
        GameId = match.GameId;
        IsWin = match.IsWin;
        IsRemake = match.GameDuration > 0 && match.GameDuration < 240; // 4分钟内过早结束视为重开
        QueueId = match.QueueId;
        ChampionId = match.ChampionId;
        Kills = match.Kills;
        Deaths = match.Deaths;
        Assists = match.Assists;
        TotalDamage = match.TotalDamageDealtToChampions;

        // 格式化文本
        FormatBasicInfo(match);

        // 初始化 6 个装备槽
        for (var i = 0; i < 6; i++)
            EquipmentSlots.Add(new MatchItemSlotViewModel());

        // 初始化符文列表
        var safeAugments = match.SafeAugments;
        HasAugments = safeAugments.Count > 0;
        AugmentCount = safeAugments.Count;

        // 异步加载所有静态资产（头像、技能、装备、符文）
        _ = LoadAssetsAsync(match, _cts.Token);
    }

    public UnifiedMatchSummary MatchSummary { get; }
    public long GameId { get; }
    public int QueueId { get; }
    public int ChampionId { get; }

    [ObservableProperty] public partial bool IsWin { get; set; }
    [ObservableProperty] public partial bool IsRemake { get; set; }
    [ObservableProperty] public partial string ModeText { get; set; } = "对局";
    [ObservableProperty] public partial string DurationText { get; set; } = "00:00";
    [ObservableProperty] public partial string TimeAgoText { get; set; } = string.Empty;

    [ObservableProperty] public partial string ChampionName { get; set; } = string.Empty;
    [ObservableProperty] public partial Bitmap? ChampionAvatar { get; set; }

    [ObservableProperty] public partial int Kills { get; set; }
    [ObservableProperty] public partial int Deaths { get; set; }
    [ObservableProperty] public partial int Assists { get; set; }
    [ObservableProperty] public partial string KdaText { get; set; } = "0 / 0 / 0";
    [ObservableProperty] public partial string DamageText { get; set; } = "0k 伤";
    [ObservableProperty] public partial long TotalDamage { get; set; }

    // 装备槽位 (6格)
    public ObservableCollection<MatchItemSlotViewModel> EquipmentSlots { get; } = [];

    // 技能与海克斯
    [ObservableProperty] public partial bool HasAugments { get; set; }
    [ObservableProperty] public partial int AugmentCount { get; set; }
    [ObservableProperty] public partial bool IsStandardAugments { get; set; }
    [ObservableProperty] public partial bool IsFiveAugments { get; set; }
    public ObservableCollection<MatchAugmentSlotViewModel> AugmentSlots { get; } = [];
    public ObservableCollection<MatchAugmentSlotViewModel> AugmentsRow1 { get; } = [];
    public ObservableCollection<MatchAugmentSlotViewModel> AugmentsRow2 { get; } = [];

    // 召唤师技能 (当无海克斯时)
    [ObservableProperty] public partial int Spell1Id { get; set; }
    [ObservableProperty] public partial int Spell2Id { get; set; }
    [ObservableProperty] public partial Bitmap? Spell1Icon { get; set; }
    [ObservableProperty] public partial Bitmap? Spell2Icon { get; set; }
    [ObservableProperty] public partial string Spell1Name { get; set; } = string.Empty;
    [ObservableProperty] public partial string Spell2Name { get; set; } = string.Empty;

    // 综合 ToolTip 战报
    [ObservableProperty] public partial string DetailedTooltip { get; set; } = string.Empty;

    [RelayCommand]
    public void OpenDetail()
    {
        if (_openWindows.TryGetValue(GameId, out var existing))
        {
            if (existing.IsVisible)
            {
                existing.Activate();
                return;
            }

            _openWindows.TryRemove(GameId, out _);
        }

        var detailVm = new MatchDetailViewModel(MatchSummary, _localPuuid, _coordinator);
        var window = new MatchDetailWindow
        {
            DataContext = detailVm
        };
        _openWindows[GameId] = window;
        EventHandler? onClosed = null;
        onClosed = (_, _) =>
        {
            window.Closed -= onClosed;
            _openWindows.TryRemove(GameId, out _);
        };
        window.Closed += onClosed;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: { } mainWin
            })
            window.Show(mainWin);
        else
            window.Show();
    }

    private void FormatBasicInfo(UnifiedMatchSummary match)
    {
        // 1. 模式映射
        ModeText = ResolveQueueName(match.QueueId, match.GameMode, match.SafeAugments.Count > 0);

        // 2. 时长 (mm:ss)
        var min = match.GameDuration / 60;
        var sec = match.GameDuration % 60;
        DurationText = $"{min:D2}:{sec:D2}";

        // 3. 相对时间
        TimeAgoText = FormatTimeAgo(match.GameCreation);

        // 4. 英雄名称
        ChampionName = _coordinator.ChampionStaticData.GetChampionName(match.ChampionId);

        // 5. KDA 与伤害 (如 10.3k 伤)
        KdaText = $"{match.Kills} / {match.Deaths} / {match.Assists}";
        if (match.TotalDamageDealtToChampions >= 1000)
            DamageText = $"{match.TotalDamageDealtToChampions / 1000.0:F1}k 伤";
        else
            DamageText = $"{match.TotalDamageDealtToChampions} 伤";

        Spell1Id = match.Spell1Id;
        Spell2Id = match.Spell2Id;
        Spell1Name = _coordinator.SummonerSpellStaticData.GetSpellName(match.Spell1Id);
        Spell2Name = _coordinator.SummonerSpellStaticData.GetSpellName(match.Spell2Id);

        // 构建悬浮详细战报
        var kdaRatio = match.Deaths == 0 ? "完美 (0死)" : $"{match.Kda:F2}";
        DetailedTooltip = $"{ModeText} · {(IsRemake ? "重开" : IsWin ? "胜利" : "失利")}\n" +
                          $"英雄：{ChampionName}\n" +
                          $"战绩：{KdaText} (KDA: {kdaRatio})\n" +
                          $"对英雄总伤害：{match.TotalDamageDealtToChampions:N0}\n" +
                          $"总承受伤害：{match.TotalDamageTaken:N0}\n" +
                          $"赚取金币：{match.GoldEarned:N0}\n" +
                          $"时长：{DurationText} ({TimeAgoText})";
    }

    private async Task LoadAssetsAsync(UnifiedMatchSummary match, CancellationToken ct)
    {
        // ① 加载英雄肖像
        var avatarTask = ChampionIconLoader.GetIconAsync(match.ChampionId, _coordinator, ct);

        // ② 加载召唤师技能 (若无海克斯)
        Task<Bitmap?>? s1Task = null;
        Task<Bitmap?>? s2Task = null;
        if (!HasAugments)
        {
            if (match.Spell1Id > 0)
                s1Task = SummonerSpellIconLoader.GetIconAsync(match.Spell1Id, _coordinator, ct);
            if (match.Spell2Id > 0)
                s2Task = SummonerSpellIconLoader.GetIconAsync(match.Spell2Id, _coordinator, ct);
        }

        // ③ 加载装备 (前 6 件)
        var itemTasks = new List<Task<Bitmap?>>(6);
        for (var i = 0; i < 6; i++)
        {
            var itemId = match.Items != null && i < match.Items.Count ? match.Items[i] : 0;
            itemTasks.Add(itemId > 0
                ? ItemIconLoader.GetIconAsync(itemId, _coordinator, ct)
                : Task.FromResult<Bitmap?>(null));
        }

        // ④ 加载海克斯符文
        var augList = match.SafeAugments;
        var augTasks = new List<Task<Bitmap?>>(augList.Count);
        if (HasAugments)
            foreach (var augId in augList)
                augTasks.Add(AugmentIconLoader.GetIconAsync(augId, _coordinator, ct));

        // 等待所有资产完成
        try
        {
            var avatar = await avatarTask.ConfigureAwait(false);
            var s1 = s1Task != null ? await s1Task.ConfigureAwait(false) : null;
            var s2 = s2Task != null ? await s2Task.ConfigureAwait(false) : null;

            var itemBitmaps = new List<Bitmap?>(6);
            foreach (var it in itemTasks)
                itemBitmaps.Add(await it.ConfigureAwait(false));

            var augBitmaps = new List<Bitmap?>(augTasks.Count);
            foreach (var at in augTasks)
                augBitmaps.Add(await at.ConfigureAwait(false));

            if (ct.IsCancellationRequested)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                if (ct.IsCancellationRequested)
                    return;

                ChampionAvatar = avatar;
                Spell1Icon = s1;
                Spell2Icon = s2;

                // 填充 6 个装备槽
                for (var i = 0; i < 6; i++)
                {
                    var itemId = match.Items != null && i < match.Items.Count ? match.Items[i] : 0;
                    var slot = EquipmentSlots[i];
                    slot.ItemId = itemId;
                    slot.HasItem = itemId > 0;
                    slot.Icon = itemBitmaps[i];
                    slot.ItemName = itemId > 0 ? _coordinator.ItemStaticData.GetItemName(itemId) : string.Empty;
                }

                // 填充海克斯槽位
                if (HasAugments)
                {
                    AugmentSlots.Clear();
                    AugmentsRow1.Clear();
                    AugmentsRow2.Clear();

                    var maxAugs = Math.Min(5, augList.Count);
                    IsFiveAugments = maxAugs == 5;
                    IsStandardAugments = maxAugs is >= 1 and <= 4;

                    var slotList = new List<MatchAugmentSlotViewModel>(maxAugs);
                    for (var i = 0; i < maxAugs; i++)
                    {
                        var augId = augList[i];
                        var info = _coordinator.KiwiAugmentStaticData.GetAugmentInfo(augId);

                        IBrush borderBrush = Brushes.SlateGray;
                        IBrush bgBrush = Brushes.Transparent;
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(info?.BorderColorHex))
                                borderBrush = new SolidColorBrush(Color.Parse(info.BorderColorHex));
                            if (!string.IsNullOrWhiteSpace(info?.BackgroundColorHex))
                                bgBrush = new SolidColorBrush(Color.Parse(info.BackgroundColorHex));
                        }
                        catch
                        {
                        }

                        var slot = new MatchAugmentSlotViewModel
                        {
                            AugmentId = augId,
                            HasAugment = true,
                            Icon = i < augBitmaps.Count ? augBitmaps[i] : null,
                            Name = info?.Name ?? $"强化 {augId}",
                            Desc = info?.Desc ?? string.Empty,
                            BorderBrush = borderBrush,
                            BackgroundBrush = bgBrush
                        };
                        slotList.Add(slot);
                    }

                    if (IsFiveAugments)
                    {
                        // 5 符文：上 3 下 2 居中
                        for (var i = 0; i < 3; i++)
                            AugmentsRow1.Add(slotList[i]);
                        for (var i = 3; i < 5; i++)
                            AugmentsRow2.Add(slotList[i]);
                    }
                    else
                    {
                        // 1~4 符文：2×2 矩阵
                        foreach (var slot in slotList)
                            AugmentSlots.Add(slot);

                        // 不足 4 个补齐空槽（透明占位，不绘制深灰圆圈）
                        for (var i = maxAugs; i < 4; i++)
                            AugmentSlots.Add(new MatchAugmentSlotViewModel
                            {
                                AugmentId = 0,
                                HasAugment = false,
                                Name = "空强化槽",
                                BorderBrush = Brushes.Transparent,
                                BackgroundBrush = Brushes.Transparent
                            });
                    }
                }
            });
        }
        catch
        {
            // 忽略加载异常
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private static string ResolveQueueName(int queueId, string? gameMode, bool hasAugments)
    {
        return queueId switch
        {
            420 => "单双排位",
            440 => "灵活组排",
            450 or 2400 => hasAugments ? "海克斯大乱斗" : "极地大乱斗",
            430 => "匹配模式",
            490 => "快速匹配",
            1700 or 1710 => "斗魂竞技场",
            _ => !string.IsNullOrWhiteSpace(gameMode)
                ? gameMode.Equals("ARAM", StringComparison.OrdinalIgnoreCase) ? hasAugments ? "海克斯大乱斗" : "极地大乱斗" :
                gameMode.Equals("CHERRY", StringComparison.OrdinalIgnoreCase) ? "斗魂竞技场" : gameMode
                : "对局"
        };
    }

    private static string FormatTimeAgo(long creationMillis)
    {
        if (creationMillis <= 0)
            return string.Empty;

        var creation = DateTimeOffset.FromUnixTimeMilliseconds(creationMillis);
        var diff = DateTimeOffset.UtcNow - creation;

        if (diff.TotalMinutes < 1)
            return "刚刚";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes}分钟前";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours}小时前";
        if (diff.TotalDays < 2)
            return "昨天";
        if (diff.TotalDays < 30)
            return $"{(int)diff.TotalDays}天前";

        return creation.ToString("MM-dd", CultureInfo.InvariantCulture);
    }
}