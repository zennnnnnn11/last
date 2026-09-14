using last.Core.MatchHistory.Models;
using last.Core.State.Models;

namespace last.Core.MatchHistory.Services;

public interface IMatchHistoryService : IDisposable
{
    bool IsTokenReady { get; }

    string? ActivePlatformId { get; set; }

    void ResetTokens();

    Task<bool> RefreshTokensAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<UnifiedTeamMember> ExtractTeamMembers(ChampSelectSession session, bool includeOpponents = false);

    Task<IReadOnlyList<UnifiedTeamMember>> GetTeamMembersFromGsmAsync(
        string puuid,
        string? platformId = null,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<UnifiedMatchSummary>> GetMatchHistoryAsync(
        string puuid,
        int startIndex = 0,
        int count = 20,
        MatchModeFilter? filter = null,
        string? platformId = null,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<UnifiedMatchSummary>> GetMatchHistoryAsync(
        string puuid,
        int startIndex,
        int count,
        int? queueId,
        string? platformId = null,
        CancellationToken cancellationToken = default
    );

    Task<UnifiedMatchSummary?> GetGameSummaryAsync(
        long gameId,
        string? targetPuuid = null,
        string? platformId = null,
        CancellationToken cancellationToken = default
    );

    Task<UnifiedMatchDetails?> GetGameDetailsAsync(
        long gameId,
        string? platformId = null,
        CancellationToken cancellationToken = default
    );
}