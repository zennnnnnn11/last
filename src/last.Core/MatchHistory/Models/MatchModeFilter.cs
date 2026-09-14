namespace last.Core.MatchHistory.Models;

/// <summary>
///     对局模式筛选器（支持多队列 ID 映射与游戏模式名称容错识别）
/// </summary>
public sealed record MatchModeFilter(
    string Key,
    string DisplayName,
    IReadOnlyList<int>? QueueIds = null,
    IReadOnlyList<string>? GameModes = null
)
{
    // 常用预置模式定义
    public static readonly MatchModeFilter All = new("ALL", "全部模式");
    public static readonly MatchModeFilter RankedSolo = new("RANKED_SOLO", "单双排位", [420]);
    public static readonly MatchModeFilter RankedFlex = new("RANKED_FLEX", "灵活排位", [440]);
    public static readonly MatchModeFilter Normal = new("NORMAL", "匹配模式", [430, 490, 400], ["CLASSIC"]);
    public static readonly MatchModeFilter Aram = new("ARAM", "海克斯大乱斗", [2400, 450], ["ARAM"]);
    public static readonly MatchModeFilter Arena = new("ARENA", "斗魂竞技场", [1700, 1710], ["CHERRY"]);

    public static readonly MatchModeFilter Special = new("SPECIAL", "轮换模式", [900, 1010, 1020, 1300, 1900],
        ["URF", "ARURF", "ONEFORALL", "NEXUSBLITZ", "ULTBOOK"]);

    /// <summary>
    ///     所有可选模式列表
    /// </summary>
    public static readonly IReadOnlyList<MatchModeFilter> PresetFilters =
    [
        All,
        RankedSolo,
        RankedFlex,
        Normal,
        Aram,
        Arena,
        Special
    ];

    public bool IsAll => (QueueIds == null || QueueIds.Count == 0) &&
                         (GameModes == null || GameModes.Count == 0);

    /// <summary>
    ///     获取 SGP 服务的服务端 tag 过滤参数（如 q_420、q_450）
    /// </summary>
    public IReadOnlyList<string> SgpTags
    {
        get
        {
            if (QueueIds == null || QueueIds.Count == 0)
                return [];

            var tags = new string[QueueIds.Count];
            for (var i = 0; i < QueueIds.Count; i++)
                tags[i] = $"q_{QueueIds[i]}";
            return tags;
        }
    }

    /// <summary>
    ///     判定给定的对局是否属于当前筛选模式
    /// </summary>
    public bool Matches(int queueId, string? gameMode)
    {
        if (IsAll)
            return true;

        if (QueueIds != null)
            for (var i = 0; i < QueueIds.Count; i++)
                if (QueueIds[i] == queueId)
                    return true;

        if (GameModes != null && !string.IsNullOrWhiteSpace(gameMode))
            for (var i = 0; i < GameModes.Count; i++)
                if (gameMode.Equals(GameModes[i], StringComparison.OrdinalIgnoreCase))
                    return true;

        return false;
    }
}