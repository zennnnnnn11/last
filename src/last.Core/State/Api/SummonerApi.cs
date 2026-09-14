using last.Core.State.Models;

namespace last.Core.State.Api;

/// <summary>
///     客户端召唤师信息 REST API 实现。
/// </summary>
public sealed class SummonerApi : ISummonerApi
{
    private readonly ILcuRestClient _client;

    public SummonerApi(ILcuRestClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public Task<SummonerInfo?> GetCurrentSummonerAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<SummonerInfo?>("/lol-summoner/v1/current-summoner", cancellationToken);
    }

    public Task<SummonerProfile?> GetCurrentSummonerProfileAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<SummonerProfile?>("/lol-summoner/v1/current-summoner/summoner-profile",
            cancellationToken);
    }
}