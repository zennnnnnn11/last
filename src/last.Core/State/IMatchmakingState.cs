using last.Core.State.Models;

namespace last.Core.State;

/// <summary>
///     匹配系统响应式状态容器契约，严格对照 LeagueAkari MatchmakingState。
/// </summary>
public interface IMatchmakingState
{
    /// <summary>
    ///     获取当前对局就绪确认状态快照。
    /// </summary>
    ReadyCheck? ReadyCheck { get; }

    /// <summary>
    ///     获取当前寻局搜索状态快照。
    /// </summary>
    MatchmakingSearch? Search { get; }

    /// <summary>
    ///     当对局就绪确认状态发生变更时触发。
    /// </summary>
    event Action<ReadyCheck?>? ReadyCheckChanged;

    /// <summary>
    ///     当寻局搜索状态发生变更时触发。
    /// </summary>
    event Action<MatchmakingSearch?>? SearchChanged;

    /// <summary>
    ///     设置就绪确认状态。
    /// </summary>
    void SetReadyCheck(ReadyCheck? readyCheck);

    /// <summary>
    ///     设置寻局搜索状态。
    /// </summary>
    void SetSearch(MatchmakingSearch? search);

    /// <summary>
    ///     重置所有匹配状态（断开连接或登出时调用）。
    /// </summary>
    void Reset();
}