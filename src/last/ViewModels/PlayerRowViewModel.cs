using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using last.Core.Connection.Services;
using last.Services;

namespace last.ViewModels;

/// <summary>
///     近期对局胜负走势微点视图模型（用于 5 连圆点展示）。
/// </summary>
public sealed partial class MatchDotViewModel : ObservableObject
{
    [ObservableProperty] public partial bool IsWin { get; set; }

    [ObservableProperty] public partial bool IsLoss { get; set; }

    [ObservableProperty] public partial bool IsEmpty { get; set; } = true;

    [ObservableProperty] public partial string Tooltip { get; set; } = "待对局记录";

    public void SetWin(int index)
    {
        IsWin = true;
        IsLoss = false;
        IsEmpty = false;
        Tooltip = $"近期第 {index} 场：胜利";
    }

    public void SetLoss(int index)
    {
        IsWin = false;
        IsLoss = true;
        IsEmpty = false;
        Tooltip = $"近期第 {index} 场：失利";
    }

    public void SetEmpty()
    {
        IsWin = false;
        IsLoss = false;
        IsEmpty = true;
        Tooltip = "待对局数据";
    }
}

/// <summary>
///     纵向通栏单行玩家展示视图模型（支持 304px 通栏展示头像、技能、完整昵称与战绩点）。
/// </summary>
public sealed partial class PlayerRowViewModel : ObservableObject
{
    private readonly ILcuConnectionCoordinator _coordinator;
    private CancellationTokenSource? _champLoadCts;
    private string _lastQueriedPuuid = string.Empty;
    private CancellationTokenSource? _matchHistoryCts;
    private CancellationTokenSource? _spellLoadCts;

    public PlayerRowViewModel(ILcuConnectionCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

        for (var i = 0; i < 5; i++)
            RecentDots.Add(new MatchDotViewModel());
    }

    [ObservableProperty] public partial int ChampionId { get; set; }

    [ObservableProperty] public partial string ChampionName { get; set; } = string.Empty;

    [ObservableProperty] public partial string ChampionRole { get; set; } = string.Empty;

    [ObservableProperty] public partial bool HasRole { get; set; }

    [ObservableProperty] public partial Bitmap? ChampionAvatar { get; set; }

    [ObservableProperty] public partial bool HasChampion { get; set; }

    [ObservableProperty] public partial int Spell1Id { get; set; }

    [ObservableProperty] public partial string Spell1Name { get; set; } = string.Empty;

    [ObservableProperty] public partial Bitmap? Spell1Icon { get; set; }

    [ObservableProperty] public partial bool HasSpell1 { get; set; }

    [ObservableProperty] public partial int Spell2Id { get; set; }

    [ObservableProperty] public partial string Spell2Name { get; set; } = string.Empty;

    [ObservableProperty] public partial Bitmap? Spell2Icon { get; set; }

    [ObservableProperty] public partial bool HasSpell2 { get; set; }

    [ObservableProperty] public partial string SummonerName { get; set; } = string.Empty;

    [ObservableProperty] public partial string TagLine { get; set; } = string.Empty;

    [ObservableProperty] public partial string FullDisplayName { get; set; } = string.Empty;

    [ObservableProperty] public partial string Puuid { get; set; } = string.Empty;

    [ObservableProperty] public partial bool IsLocalPlayer { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAllySelected))]
    [NotifyPropertyChangedFor(nameof(IsEnemySelected))]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAllySelected))]
    [NotifyPropertyChangedFor(nameof(IsEnemySelected))]
    public partial bool IsEnemy { get; set; }

    public bool IsAllySelected => IsSelected && !IsEnemy;
    public bool IsEnemySelected => IsSelected && IsEnemy;

    public Action<PlayerRowViewModel>? SelectionHandler { get; set; }

    [ObservableProperty] public partial bool IsActive { get; set; }

    [ObservableProperty] public partial bool HasBalance { get; set; }

    [ObservableProperty] public partial string BalanceTooltip { get; set; } = string.Empty;

    public ObservableCollection<MatchDotViewModel> RecentDots { get; } = [];

    public ObservableCollection<BenchBalanceAdjustmentItem> BalanceAdjustments { get; } = [];

    [RelayCommand]
    public void ToggleSelect()
    {
        if (!IsActive || string.IsNullOrWhiteSpace(Puuid))
            return;

        SelectionHandler?.Invoke(this);
    }

    public void Reset()
    {
        _champLoadCts?.Cancel();
        _champLoadCts?.Dispose();
        _champLoadCts = null;

        _spellLoadCts?.Cancel();
        _spellLoadCts?.Dispose();
        _spellLoadCts = null;

        _matchHistoryCts?.Cancel();
        _matchHistoryCts?.Dispose();
        _matchHistoryCts = null;

        _lastQueriedPuuid = string.Empty;

        ChampionId = 0;
        ChampionName = string.Empty;
        ChampionRole = string.Empty;
        HasRole = false;
        ChampionAvatar = null;
        HasChampion = false;

        Spell1Id = 0;
        Spell1Name = string.Empty;
        Spell1Icon = null;
        HasSpell1 = false;

        Spell2Id = 0;
        Spell2Name = string.Empty;
        Spell2Icon = null;
        HasSpell2 = false;

        SummonerName = string.Empty;
        TagLine = string.Empty;
        FullDisplayName = string.Empty;
        Puuid = string.Empty;
        IsLocalPlayer = false;
        IsActive = false;
        IsSelected = false;
        IsEnemy = false;

        HasBalance = false;
        BalanceTooltip = string.Empty;
        BalanceAdjustments.Clear();

        foreach (var dot in RecentDots)
            dot.SetEmpty();
    }

    public void Update(
        int championId,
        int spell1Id,
        int spell2Id,
        string summonerName,
        string tagLine,
        string puuid,
        bool isLocalPlayer,
        bool isEnemy = false)
    {
        IsActive = true;
        IsLocalPlayer = isLocalPlayer;
        IsEnemy = isEnemy;
        SummonerName = string.IsNullOrWhiteSpace(summonerName) ? "召唤师" : summonerName;
        TagLine = tagLine ?? string.Empty;
        FullDisplayName = !string.IsNullOrWhiteSpace(TagLine)
            ? $"{SummonerName} #{TagLine}"
            : SummonerName;
        Puuid = puuid ?? string.Empty;

        UpdateChampion(championId);
        UpdateSpells(spell1Id, spell2Id);
        UpdateRecentMatches(puuid);
    }

    private void UpdateChampion(int championId)
    {
        var isCurrentLoaded = championId <= 0 || ChampionAvatar != null;
        if (championId == ChampionId && isCurrentLoaded)
            return;

        _champLoadCts?.Cancel();
        _champLoadCts?.Dispose();
        _champLoadCts = null;

        if (championId <= 0)
        {
            ChampionId = 0;
            ChampionName = string.Empty;
            ChampionRole = string.Empty;
            HasRole = false;
            ChampionAvatar = null;
            HasChampion = false;
            HasBalance = false;
            BalanceTooltip = string.Empty;
            BalanceAdjustments.Clear();
            return;
        }

        ChampionId = championId;
        HasChampion = true;
        ChampionName = _coordinator.ChampionStaticData.GetChampionName(championId);

        var champInfo = _coordinator.ChampionStaticData.GetChampion(championId);
        var role = FormatChampionRole(champInfo?.Roles);
        ChampionRole = role;
        HasRole = !string.IsNullOrWhiteSpace(role);

        BalanceAdjustments.Clear();
        if (_coordinator.AramBalance.HasBalance(championId))
        {
            var balance = _coordinator.AramBalance.GetBalance(championId);
            HasBalance = balance.Adjustments.Count > 0;
            if (balance.Adjustments.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"{ChampionName} (大乱斗平衡)");
                foreach (var adj in balance.Adjustments)
                {
                    sb.AppendLine($"• {adj.DisplayName}：{adj.FormattedValue}");
                    BalanceAdjustments.Add(BenchBalanceAdjustmentItem.FromAdjustment(adj));
                }

                BalanceTooltip = sb.ToString().TrimEnd();
            }
            else
            {
                BalanceTooltip = ChampionName;
            }
        }
        else
        {
            HasBalance = false;
            BalanceTooltip = ChampionName;
        }

        var cts = new CancellationTokenSource();
        _champLoadCts = cts;
        var token = cts.Token;

        _ = LoadChampionAvatarAsync(championId, token);
    }

    private async Task LoadChampionAvatarAsync(int championId, CancellationToken token)
    {
        try
        {
            var bitmap = await ChampionIconLoader.GetIconAsync(championId, _coordinator, token).ConfigureAwait(false);
            if (token.IsCancellationRequested)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                if (!token.IsCancellationRequested && ChampionId == championId)
                {
                    ChampionAvatar = bitmap;
                    if (string.IsNullOrWhiteSpace(ChampionName))
                    {
                        ChampionName = _coordinator.ChampionStaticData.GetChampionName(championId);
                        var info = _coordinator.ChampionStaticData.GetChampion(championId);
                        var r = FormatChampionRole(info?.Roles);
                        ChampionRole = r;
                        HasRole = !string.IsNullOrWhiteSpace(r);
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Failed to load champion avatar: {ex.Message}");
        }
    }

    private void UpdateSpells(int spell1Id, int spell2Id)
    {
        var spell1Loaded = spell1Id <= 0 || Spell1Icon != null;
        var spell2Loaded = spell2Id <= 0 || Spell2Icon != null;
        if (spell1Id == Spell1Id && spell2Id == Spell2Id && spell1Loaded && spell2Loaded)
            return;

        _spellLoadCts?.Cancel();
        _spellLoadCts?.Dispose();
        _spellLoadCts = null;

        Spell1Id = spell1Id;
        Spell2Id = spell2Id;
        HasSpell1 = spell1Id > 0;
        HasSpell2 = spell2Id > 0;
        Spell1Name = _coordinator.SummonerSpellStaticData.GetSpellName(spell1Id);
        Spell2Name = _coordinator.SummonerSpellStaticData.GetSpellName(spell2Id);

        var cts = new CancellationTokenSource();
        _spellLoadCts = cts;
        _ = LoadSpellsAsync(spell1Id, spell2Id, cts.Token);
    }

    private async Task LoadSpellsAsync(int spell1Id, int spell2Id, CancellationToken token)
    {
        try
        {
            var t1 = spell1Id > 0 ? SummonerSpellIconLoader.GetIconAsync(spell1Id, _coordinator, token) : null;
            var t2 = spell2Id > 0 ? SummonerSpellIconLoader.GetIconAsync(spell2Id, _coordinator, token) : null;

            var b1 = t1 != null ? await t1.ConfigureAwait(false) : null;
            var b2 = t2 != null ? await t2.ConfigureAwait(false) : null;

            if (token.IsCancellationRequested)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                if (!token.IsCancellationRequested && Spell1Id == spell1Id && Spell2Id == spell2Id)
                {
                    Spell1Icon = b1;
                    Spell2Icon = b2;
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Failed to load summoner spells: {ex.Message}");
        }
    }

    private void UpdateRecentMatches(string? puuid)
    {
        if (string.IsNullOrWhiteSpace(puuid))
        {
            foreach (var dot in RecentDots)
                dot.SetEmpty();
            return;
        }

        if (string.Equals(_lastQueriedPuuid, puuid, StringComparison.Ordinal))
            return;

        _matchHistoryCts?.Cancel();
        _matchHistoryCts?.Dispose();
        _matchHistoryCts = null;

        _lastQueriedPuuid = puuid;

        var cts = new CancellationTokenSource();
        _matchHistoryCts = cts;
        _ = LoadRecentMatchesAsync(puuid, cts.Token);
    }

    private async Task LoadRecentMatchesAsync(string puuid, CancellationToken token)
    {
        try
        {
            var matches = await _coordinator.MatchHistory
                .GetMatchHistoryAsync(puuid, 0, 5, cancellationToken: token)
                .ConfigureAwait(false);

            if (token.IsCancellationRequested)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                if (token.IsCancellationRequested || !string.Equals(Puuid, puuid, StringComparison.Ordinal))
                    return;

                if ((SummonerName.StartsWith("队友 ") || SummonerName.StartsWith("对手 ") || SummonerName == "召唤师") &&
                    matches is { Count: > 0 } &&
                    matches[0].Participants is { Count: > 0 } participants)
                {
                    var self = participants.FirstOrDefault(p =>
                        string.Equals(p.Puuid, puuid, StringComparison.OrdinalIgnoreCase));
                    if (self is not null && !string.IsNullOrWhiteSpace(self.SummonerName))
                    {
                        var realName = self.SummonerName;
                        var tag = string.Empty;
                        if (realName.Contains('#'))
                        {
                            var parts = realName.Split('#', 2);
                            realName = parts[0];
                            tag = parts[1];
                        }

                        SummonerName = realName;
                        if (!string.IsNullOrWhiteSpace(tag))
                            TagLine = tag;
                        FullDisplayName = !string.IsNullOrWhiteSpace(TagLine)
                            ? $"{SummonerName} #{TagLine}"
                            : SummonerName;
                    }
                }

                for (var i = 0; i < 5; i++)
                    if (matches != null && i < matches.Count)
                    {
                        if (matches[i].IsWin)
                            RecentDots[i].SetWin(i + 1);
                        else
                            RecentDots[i].SetLoss(i + 1);
                    }
                    else
                    {
                        RecentDots[i].SetEmpty();
                    }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (!token.IsCancellationRequested)
                Dispatcher.UIThread.Post(() =>
                {
                    foreach (var dot in RecentDots)
                        dot.SetEmpty();
                });
        }
    }

    private static string FormatChampionRole(IReadOnlyList<string>? roles)
    {
        if (roles is null or { Count: 0 })
            return string.Empty;

        for (var i = 0; i < roles.Count; i++)
        {
            var r = roles[i];
            if (string.IsNullOrWhiteSpace(r))
                continue;

            var role = r.Trim().ToLowerInvariant();
            switch (role)
            {
                case "mage" or "法师":
                    return "法师";
                case "assassin" or "刺客":
                    return "刺客";
                case "fighter" or "战士":
                    return "战士";
                case "marksman" or "射手" or "adc":
                    return "射手";
                case "tank" or "坦克":
                    return "坦克";
                case "support" or "辅助":
                    return "辅助";
            }
        }

        return roles[0].Trim();
    }
}