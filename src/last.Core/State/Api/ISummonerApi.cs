using last.Core.State.Models;

namespace last.Core.State.Api;

/// <summary>
///     客户端召唤师信息 REST API 契约。
/// </summary>
public interface ISummonerApi
{
    /// <summary>
    ///     获取当前已登录召唤师详细信息 (/lol-summoner/v1/current-summoner)。
    /// </summary>
    Task<SummonerInfo?> GetCurrentSummonerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     获取当前登录召唤师的个人资料背景装饰 (/lol-summoner/v1/current-summoner/summoner-profile)。
    /// </summary>
    Task<SummonerProfile?> GetCurrentSummonerProfileAsync(CancellationToken cancellationToken = default);
}