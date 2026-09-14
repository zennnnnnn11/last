using last.Core.MatchHistory.Models;

namespace last.Core.MatchHistory.Api;

public interface ISgpMatchHistoryClient : IDisposable
{
    Task<SgpMatchHistoryLol?> GetMatchHistorySummaryAsync(
        string puuid,
        string accessToken,
        string? platformId = null,
        int startIndex = 0,
        int count = 20,
        IReadOnlyList<string>? tags = null,
        string? tagsQueryType = null,
        CancellationToken cancellationToken = default
    );

    Task<SgpGameSummaryLol?> GetGameSummaryAsync(
        long gameId,
        string accessToken,
        string? platformId = null,
        CancellationToken cancellationToken = default
    );

    Task<SgpGameDetailsLol?> GetGameDetailsAsync(
        long gameId,
        string accessToken,
        string? platformId = null,
        CancellationToken cancellationToken = default
    );

    Task<SgpGsmLedgeRegion?> GetGsmByPuuidAsync(
        string puuid,
        string sessionToken,
        string? platformId = null,
        CancellationToken cancellationToken = default
    );
}