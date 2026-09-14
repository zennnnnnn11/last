using last.Core.State.Models;

namespace last.Core.State;

/// <summary>
///     客户端召唤师资料响应式状态契约，严格对照 LeagueAkari SummonerState。
/// </summary>
public interface ISummonerState
{
    /// <summary>
    ///     当前已登录的召唤师信息。为对齐原版引用习惯，使用 Me。
    /// </summary>
    SummonerInfo? Me { get; }

    /// <summary>
    ///     当前召唤师资料背景装饰。
    /// </summary>
    SummonerProfile? Profile { get; }

    /// <summary>
    ///     该大区是否启用了新 ID 系统（如 雪之下雪乃#10000）。
    /// </summary>
    bool NewIdSystemEnabled { get; }

    /// <summary>
    ///     当前召唤师状态变更通知。
    /// </summary>
    event Action<SummonerInfo?>? CurrentSummonerChanged;

    /// <summary>
    ///     召唤师背景资料变更通知。
    /// </summary>
    event Action<SummonerProfile?>? ProfileChanged;

    /// <summary>
    ///     更新当前登录召唤师。
    /// </summary>
    void SetMe(SummonerInfo? value);

    /// <summary>
    ///     更新召唤师背景资料。
    /// </summary>
    void SetProfile(SummonerProfile? value);

    /// <summary>
    ///     重置状态至初始默认值（Me = null, Profile = null, NewIdSystemEnabled = false）。
    /// </summary>
    void Reset();
}