using last.Core.MatchHistory.Models;
using last.Core.State.Api;

namespace last.Core.MatchHistory.Api;

public sealed class LcuMatchHistoryApi : ILcuMatchHistoryApi
{
    private readonly ILcuRestClient _client;

    public LcuMatchHistoryApi(ILcuRestClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public Task<LcuMatchHistory?> GetMatchHistoryAsync(string puuid, int begIndex = 0, int endIndex = 19,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(puuid);
        var uri = $"/lol-match-history/v1/products/lol/{puuid}/matches?begIndex={begIndex}&endIndex={endIndex}";
        return _client.GetAsync<LcuMatchHistory>(uri, cancellationToken);
    }

    public Task<LcuGameSummary?> GetGameAsync(long gameId, CancellationToken cancellationToken = default)
    {
        var uri = $"/lol-match-history/v1/games/{gameId}";
        return _client.GetAsync<LcuGameSummary>(uri, cancellationToken);
    }

    public Task<LcuGameTimeline?> GetGameTimelineAsync(long gameId, CancellationToken cancellationToken = default)
    {
        var uri = $"/lol-match-history/v1/game-timelines/{gameId}";
        return _client.GetAsync<LcuGameTimeline>(uri, cancellationToken);
    }

    public Task<EntitlementsToken?> GetEntitlementsTokenAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<EntitlementsToken>("/entitlements/v1/token", cancellationToken);
    }

    public Task<string?> GetLeagueSessionTokenAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<string>("/lol-league-session/v1/league-session-token", cancellationToken);
    }
}