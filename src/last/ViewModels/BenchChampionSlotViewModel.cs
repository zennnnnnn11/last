using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using last.Core.Connection.Services;
using last.Core.GameData.Balance;
using last.Services;

namespace last.ViewModels;

public sealed record BenchBalanceAdjustmentItem(
    string DisplayName,
    string FormattedValue,
    bool IsBuff,
    bool IsNerf,
    bool IsNeutral)
{
    public static BenchBalanceAdjustmentItem FromAdjustment(AramBalanceAdjustment adj)
    {
        var isBuff = adj.Effect == AramBalanceAdjustmentEffect.Buffed;
        var isNerf = adj.Effect == AramBalanceAdjustmentEffect.Nerfed;
        return new BenchBalanceAdjustmentItem(
            adj.DisplayName,
            adj.FormattedValue,
            isBuff,
            isNerf,
            !isBuff && !isNerf);
    }
}

public sealed partial class BenchChampionSlotViewModel : ObservableObject
{
    private readonly ILcuConnectionCoordinator _coordinator;
    private CancellationTokenSource? _loadCts;

    public BenchChampionSlotViewModel(ILcuConnectionCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    [ObservableProperty] public partial int ChampionId { get; set; }
    [ObservableProperty] public partial string ChampionName { get; set; } = string.Empty;
    [ObservableProperty] public partial string ChampionRole { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasRole { get; set; }
    [ObservableProperty] public partial Bitmap? IconBitmap { get; set; }
    [ObservableProperty] public partial bool HasChampion { get; set; }
    [ObservableProperty] public partial bool IsPriority { get; set; }
    [ObservableProperty] public partial bool HasBalance { get; set; }
    [ObservableProperty] public partial string BalanceBadgeText { get; set; } = string.Empty;
    [ObservableProperty] public partial string BalanceTooltip { get; set; } = string.Empty;

    public ObservableCollection<BenchBalanceAdjustmentItem> BalanceAdjustments { get; } = [];

    [ObservableProperty]
    public partial AramBalanceOverallEffect BalanceEffect { get; set; } = AramBalanceOverallEffect.Neutral;

    [ObservableProperty] public partial bool IsBuff { get; set; }
    [ObservableProperty] public partial bool IsNerf { get; set; }
    [ObservableProperty] public partial bool IsNeutral { get; set; }
    [ObservableProperty] public partial bool IsSwapping { get; set; }

    [RelayCommand]
    private async Task SwapAsync()
    {
        if (ChampionId <= 0 || IsSwapping)
            return;

        IsSwapping = true;
        try
        {
            await _coordinator.BenchSwap.SwapAsync(ChampionId).ConfigureAwait(false);
        }
        finally
        {
            Dispatcher.UIThread.Post(() => IsSwapping = false);
        }
    }

    public void Clear()
    {
        Update(0, false);
    }

    public void Update(int championId, bool isPriority)
    {
        if (championId == ChampionId && isPriority == IsPriority)
            return;

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;

        if (championId <= 0)
        {
            ChampionId = 0;
            ChampionName = string.Empty;
            ChampionRole = string.Empty;
            HasRole = false;
            IconBitmap = null;
            HasChampion = false;
            IsPriority = false;
            HasBalance = false;
            IsBuff = false;
            IsNerf = false;
            IsNeutral = false;
            BalanceBadgeText = string.Empty;
            BalanceTooltip = string.Empty;
            BalanceEffect = AramBalanceOverallEffect.Neutral;
            BalanceAdjustments.Clear();
            return;
        }

        ChampionId = championId;
        HasChampion = true;
        IsPriority = isPriority;
        ChampionName = _coordinator.ChampionStaticData.GetChampionName(championId);

        var champInfo = _coordinator.ChampionStaticData.GetChampion(championId);
        var role = FormatChampionRole(champInfo?.Roles);
        ChampionRole = role;
        HasRole = !string.IsNullOrWhiteSpace(role);

        BalanceAdjustments.Clear();

        // 获取大乱斗平衡性调整数据
        if (_coordinator.AramBalance.HasBalance(championId))
        {
            var balance = _coordinator.AramBalance.GetBalance(championId);
            HasBalance = balance.Adjustments.Count > 0;
            BalanceEffect = balance.OverallEffect;
            IsBuff = balance.OverallEffect == AramBalanceOverallEffect.Buffed;
            IsNerf = balance.OverallEffect == AramBalanceOverallEffect.Nerfed;
            IsNeutral = HasBalance && !IsBuff && !IsNerf;

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

                var firstAdj = balance.Adjustments[0];
                BalanceBadgeText = firstAdj.FormattedValue;
            }
            else
            {
                BalanceTooltip = ChampionName;
                BalanceBadgeText = string.Empty;
            }
        }
        else
        {
            HasBalance = false;
            IsBuff = false;
            IsNerf = false;
            IsNeutral = false;
            BalanceEffect = AramBalanceOverallEffect.Neutral;
            BalanceBadgeText = string.Empty;
            BalanceTooltip = ChampionName;
        }

        // 异步加载英雄图标
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        _ = LoadIconAsync(championId, token);
    }

    private async Task LoadIconAsync(int championId, CancellationToken token)
    {
        try
        {
            var bitmap = await ChampionIconLoader.GetIconAsync(championId, _coordinator, token).ConfigureAwait(false);
            if (!token.IsCancellationRequested)
                Dispatcher.UIThread.Post(() =>
                {
                    if (!token.IsCancellationRequested && ChampionId == championId)
                    {
                        IconBitmap = bitmap;
                        if (!HasRole)
                        {
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
            AppLogger.Warn($"Failed to load bench icon: {ex.Message}");
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