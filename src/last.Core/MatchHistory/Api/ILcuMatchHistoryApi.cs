using last.Core.MatchHistory.Models;

namespace last.Core.MatchHistory.Api;

public interface ILcuMatchHistoryApi
{
    Task<LcuMatchHistory?> GetMatchHistoryAsync(string puuid, int begIndex = 0, int endIndex = 19,
        CancellationToken cancellationToken = default);

    Task<LcuGameSummary?> GetGameAsync(long gameId, CancellationToken cancellationToken = default);

    Task<LcuGameTimeline?> GetGameTimelineAsync(long gameId, CancellationToken cancellationToken = default);

    Task<EntitlementsToken?> GetEntitlementsTokenAsync(CancellationToken cancellationToken = default);

    Task<string?> GetLeagueSessionTokenAsync(CancellationToken cancellationToken = default);
}