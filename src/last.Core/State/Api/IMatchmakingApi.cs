using last.Core.State.Models;

namespace last.Core.State.Api;

/// <summary>
///     匹配系统与对局就绪确认 REST API 契约。
/// </summary>
public interface IMatchmakingApi
{
    /// <summary>
    ///     获取当前对局就绪确认状态 (/lol-matchmaking/v1/ready-check)。
    /// </summary>
    Task<ReadyCheck?> GetReadyCheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     接受对局 (/lol-matchmaking/v1/ready-check/accept)。
    /// </summary>
    Task<bool> AcceptReadyCheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     获取当前匹配寻局状态与队列信息 (/lol-matchmaking/v1/search)。
    /// </summary>
    Task<MatchmakingSearch?> GetSearchAsync(CancellationToken cancellationToken = default);
}