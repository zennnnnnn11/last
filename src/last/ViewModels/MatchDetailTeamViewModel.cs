using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace last.ViewModels;

/// <summary>
///     对局详情弹窗单个阵营队伍视图模型（蓝方/红方）。
/// </summary>
public sealed partial class MatchDetailTeamViewModel : ObservableObject
{
    public MatchDetailTeamViewModel(
        int teamId,
        bool isAlly,
        bool isWin,
        int totalKills,
        long totalGold,
        long totalDamage)
    {
        TeamId = teamId;
        IsAlly = isAlly;
        IsWin = isWin;

        TeamTitle = isAlly ? "友方队伍 (蓝方)" : "敌方队伍 (红方)";
        ResultText = isWin ? "胜利" : "败北";
        AccentBrush = isAlly ? Brush.Parse("#2563EB") : Brush.Parse("#DC2626");
        ResultBrush = isWin ? Brush.Parse("#059669") : Brush.Parse("#DC2626");

        TotalKills = totalKills;
        TotalGoldText = FormatK(totalGold);
        TotalDamageText = FormatK(totalDamage);
    }

    public int TeamId { get; }
    public bool IsAlly { get; }
    public string TeamTitle { get; }
    public IBrush AccentBrush { get; }

    [ObservableProperty] public partial bool IsWin { get; set; }
    [ObservableProperty] public partial string ResultText { get; set; }
    [ObservableProperty] public partial IBrush ResultBrush { get; set; }
    [ObservableProperty] public partial int TotalKills { get; set; }
    [ObservableProperty] public partial string TotalGoldText { get; set; }
    [ObservableProperty] public partial string TotalDamageText { get; set; }

    public ObservableCollection<MatchDetailParticipantViewModel> Participants { get; } = [];

    public void UpdateStats(int kills, long gold, long damage, bool isWin)
    {
        TotalKills = kills;
        TotalGoldText = FormatK(gold);
        TotalDamageText = FormatK(damage);
        IsWin = isWin;
        ResultText = isWin ? "胜利" : "败北";
        ResultBrush = isWin ? Brush.Parse("#059669") : Brush.Parse("#DC2626");
    }

    private static string FormatK(long val)
    {
        return val switch
        {
            >= 1_000_000 => $"{(double)val / 1_000_000:0.0}m",
            >= 1_000 => $"{(double)val / 1_000:0.0}k",
            _ => val.ToString()
        };
    }
}