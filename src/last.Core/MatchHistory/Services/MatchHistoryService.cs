using last.Core.Common;
using last.Core.MatchHistory.Adapters;
using last.Core.MatchHistory.Api;
using last.Core.MatchHistory.Models;
using last.Core.State.Models;

namespace last.Core.MatchHistory.Services;

public sealed class MatchHistoryService : IMatchHistoryService
{
    private readonly string _defaultPlatformId;
    private readonly LruCache<long, UnifiedMatchDetails> _detailsCache = new(15);
    private readonly ILcuMatchHistoryApi _lcuApi;
    private readonly ISgpMatchHistoryClient _sgpClient;
    private readonly LruCache<(long GameId, string TargetPuuid), UnifiedMatchSummary> _summaryCache = new(60);

    private volatile string? _accessToken;
    private volatile string? _activePlatformId;
    private bool _disposed;
    private volatile string? _sessionToken;
    private DateTime _sgpUnavailableUntil = DateTime.MinValue;

    public MatchHistoryService(
        ILcuMatchHistoryApi lcuApi,
        ISgpMatchHistoryClient sgpClient,
        string defaultPlatformId = TencentSgpServerConfig.DefaultPlatformId)
    {
        _lcuApi = lcuApi ?? throw new ArgumentNullException(nameof(lcuApi));
        _sgpClient = sgpClient ?? throw new ArgumentNullException(nameof(sgpClient));
        _defaultPlatformId = TencentSgpServerConfig.NormalizePlatformId(defaultPlatformId);
    }

    public bool IsTokenReady => !string.IsNullOrWhiteSpace(_accessToken);

    public string? ActivePlatformId
    {
        get => _activePlatformId;
        set
        {
            var normalized = !string.IsNullOrWhiteSpace(value)
                ? TencentSgpServerConfig.NormalizePlatformId(value)
                : null;
            if (!string.Equals(_activePlatformId, normalized, StringComparison.OrdinalIgnoreCase))
            {
                _activePlatformId = normalized;
                ResetTokens();
            }
        }
    }

    public void ResetTokens()
    {
        _accessToken = null;
        _sessionToken = null;
        _sgpUnavailableUntil = DateTime.MinValue;
        _summaryCache.Clear();
        _detailsCache.Clear();
    }

    public async Task<bool> RefreshTokensAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var entitlementsTask = _lcuApi.GetEntitlementsTokenAsync(cancellationToken);
            var sessionTask = _lcuApi.GetLeagueSessionTokenAsync(cancellationToken);

            await Task.WhenAll(entitlementsTask, sessionTask).ConfigureAwait(false);

            var entitlements = await entitlementsTask.ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(entitlements?.AccessToken))
                _accessToken = entitlements.AccessToken;

            var session = await sessionTask.ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(session))
                _sessionToken = session;

            return IsTokenReady;
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<UnifiedTeamMember> ExtractTeamMembers(ChampSelectSession session,
        bool includeOpponents = false)
    {
        ArgumentNullException.ThrowIfNull(session);

        var members = new List<UnifiedTeamMember>();

        if (session.MyTeam != null)
            foreach (var p in session.MyTeam)
                members.Add(new UnifiedTeamMember(
                    p.Puuid,
                    p.SummonerId,
                    string.Empty,
                    p.CellId,
                    p.ChampionId,
                    p.Team
                ));

        if (includeOpponents && session.TheirTeam != null)
            foreach (var p in session.TheirTeam)
                members.Add(new UnifiedTeamMember(
                    p.Puuid,
                    p.SummonerId,
                    string.Empty,
                    p.CellId,
                    p.ChampionId,
                    p.Team
                ));

        return members;
    }

    public async Task<IReadOnlyList<UnifiedTeamMember>> GetTeamMembersFromGsmAsync(
        string puuid,
        string? platformId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(puuid);
        var effectivePlatform = ResolveEffectivePlatform(platformId);

        var gsm = await ExecuteWithSessionTokenRetryAsync(
            token => _sgpClient.GetGsmByPuuidAsync(puuid, token, effectivePlatform, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return gsm != null ? MatchDataAdapter.GsmToUnified(gsm) : Array.Empty<UnifiedTeamMember>();
    }

    public Task<IReadOnlyList<UnifiedMatchSummary>> GetMatchHistoryAsync(
        string puuid,
        int startIndex,
        int count,
        int? queueId,
        string? platformId = null,
        CancellationToken cancellationToken = default)
    {
        var filter = queueId.HasValue
            ? new MatchModeFilter("CUSTOM", $"Queue_{queueId.Value}", [queueId.Value])
            : MatchModeFilter.All;
        return GetMatchHistoryAsync(puuid, startIndex, count, filter, platformId, cancellationToken);
    }

    public async Task<IReadOnlyList<UnifiedMatchSummary>> GetMatchHistoryAsync(
        string puuid,
        int startIndex = 0,
        int count = 20,
        MatchModeFilter? filter = null,
        string? platformId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(puuid);

        filter ??= MatchModeFilter.All;

        // 1. 优先走 SGP 服务端直通/服务端 Tag 过滤路径（全部模式、单/多队列模式由网关服务端精准分页，1 次请求单页命中）
        if (DateTime.UtcNow >= _sgpUnavailableUntil && (filter.IsAll || filter.SgpTags.Count > 0))
        {
            var effectivePlatform = ResolveEffectivePlatform(platformId);
            var tags = filter.IsAll ? null : filter.SgpTags;

            var sgpResult = await ExecuteWithAccessTokenRetryAsync(
                token => _sgpClient.GetMatchHistorySummaryAsync(
                    puuid, token, effectivePlatform, startIndex, count, tags, null, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            if (sgpResult?.Games != null)
            {
                var unifiedList = new List<UnifiedMatchSummary>(sgpResult.Games.Count);
                foreach (var game in sgpResult.Games)
                {
                    var u = MatchDataAdapter.SgpToUnified(game, puuid);
                    if (filter.Matches(u.QueueId, u.GameMode))
                        unifiedList.Add(u);
                }

                return unifiedList;
            }
        }

        // 2. 本地 LCU 兜底：LCU 本地缓存通常仅存 20 场，仅当 startIndex == 0 时有效拉取，避免深分页重复数据
        if (startIndex == 0)
            try
            {
                var lcuResult = await _lcuApi.GetMatchHistoryAsync(puuid, 0, count - 1, cancellationToken)
                    .ConfigureAwait(false);

                if (lcuResult?.Games.Games != null)
                {
                    var games = lcuResult.Games.Games;
                    var unifiedList = new List<UnifiedMatchSummary>(games.Count);
                    foreach (var g in games)
                    {
                        var u = MatchDataAdapter.LcuToUnified(g, puuid);
                        if (filter.Matches(u.QueueId, u.GameMode))
                            unifiedList.Add(u);
                    }

                    if (unifiedList.Count > 0 || filter.IsAll)
                        return unifiedList;
                }
            }
            catch
            {
            }

        // 3. 仅针对无 QueueId 的纯 GameMode 特殊模式，启用保底回溯扫描
        if (!filter.IsAll && filter.SgpTags.Count == 0)
            try
            {
                var matchingGames = new List<UnifiedMatchSummary>(count);
                var rawIndex = 0;
                var skippedCount = 0;
                const int batchSize = 50;
                var maxRawScan = Math.Max(200, (startIndex + count) * 10);

                var useSgp = DateTime.UtcNow >= _sgpUnavailableUntil;
                var effectivePlatform = ResolveEffectivePlatform(platformId);

                while (rawIndex < maxRawScan && matchingGames.Count < count)
                {
                    IReadOnlyList<UnifiedMatchSummary> batchSummaries = Array.Empty<UnifiedMatchSummary>();

                    if (useSgp)
                    {
                        var sgpResult = await ExecuteWithAccessTokenRetryAsync(
                            token => _sgpClient.GetMatchHistorySummaryAsync(
                                puuid, token, effectivePlatform, rawIndex, batchSize, null, null, cancellationToken),
                            cancellationToken).ConfigureAwait(false);

                        if (sgpResult?.Games != null && sgpResult.Games.Count > 0)
                        {
                            var list = new List<UnifiedMatchSummary>(sgpResult.Games.Count);
                            foreach (var game in sgpResult.Games)
                                list.Add(MatchDataAdapter.SgpToUnified(game, puuid));
                            batchSummaries = list;
                        }
                        else
                        {
                            useSgp = false;
                        }
                    }

                    if (!useSgp && rawIndex == 0)
                    {
                        var lcuBatch = await _lcuApi.GetMatchHistoryAsync(puuid, 0, 19, cancellationToken)
                            .ConfigureAwait(false);
                        var games = lcuBatch?.Games.Games;
                        if (games != null && games.Count > 0)
                        {
                            var list = new List<UnifiedMatchSummary>(games.Count);
                            foreach (var g in games)
                                list.Add(MatchDataAdapter.LcuToUnified(g, puuid));
                            batchSummaries = list;
                        }
                    }

                    if (batchSummaries.Count == 0)
                        break;

                    foreach (var g in batchSummaries)
                    {
                        if (!filter.Matches(g.QueueId, g.GameMode))
                            continue;

                        if (skippedCount < startIndex)
                        {
                            skippedCount++;
                            continue;
                        }

                        matchingGames.Add(g);
                        if (matchingGames.Count >= count)
                            break;
                    }

                    if (batchSummaries.Count < batchSize || !useSgp)
                        break;

                    rawIndex += batchSummaries.Count;
                }

                return matchingGames;
            }
            catch
            {
            }

        return Array.Empty<UnifiedMatchSummary>();
    }

    public async Task<UnifiedMatchSummary?> GetGameSummaryAsync(
        long gameId,
        string? targetPuuid = null,
        string? platformId = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = (gameId, targetPuuid ?? string.Empty);
        if (_summaryCache.TryGetValue(cacheKey, out var cached) && cached is { Participants.Count: > 1 })
            return cached;

        var effectivePlatform = ResolveEffectivePlatform(platformId);

        // 1. 优先尝试 SGP 云端接口（要求包含完整的全量对局玩家数据）
        try
        {
            var sgpSummary = await ExecuteWithAccessTokenRetryAsync(
                token => _sgpClient.GetGameSummaryAsync(gameId, token, effectivePlatform, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            if (sgpSummary?.Json?.Participants != null && sgpSummary.Json.Participants.Count > 1)
            {
                var item = new SgpGameItem(sgpSummary.Metadata, sgpSummary.Json);
                var unified = MatchDataAdapter.SgpToUnified(item, targetPuuid);
                if (unified.Participants.Count > 1)
                {
                    _summaryCache.Set(cacheKey, unified);
                    return unified;
                }
            }
        }
        catch
        {
            // SGP 请求失败或超时，无缝降级到 LCU 客户端接口
        }

        // 2. 兜底尝试 LCU 本地客户端接口（LCU 拥有官方客户端完整的 10 位对局参与者数据）
        try
        {
            var lcuGame = await _lcuApi.GetGameAsync(gameId, cancellationToken).ConfigureAwait(false);
            if (lcuGame != null)
            {
                var unified = MatchDataAdapter.LcuToUnified(lcuGame, targetPuuid);
                if (unified.Participants.Count > 1) _summaryCache.Set(cacheKey, unified);
                return unified;
            }
        }
        catch
        {
        }

        return null;
    }

    public async Task<UnifiedMatchDetails?> GetGameDetailsAsync(
        long gameId,
        string? platformId = null,
        CancellationToken cancellationToken = default)
    {
        if (_detailsCache.TryGetValue(gameId, out var cached))
            return cached;

        var effectivePlatform = ResolveEffectivePlatform(platformId);

        // 1. SGP 云端优先
        if (string.IsNullOrWhiteSpace(_accessToken))
            await RefreshTokensAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(_accessToken))
            try
            {
                var sgpDetails = await _sgpClient
                    .GetGameDetailsAsync(gameId, _accessToken, effectivePlatform, cancellationToken)
                    .ConfigureAwait(false);

                if (sgpDetails?.Json != null)
                {
                    var unified = MatchDataAdapter.SgpDetailsToUnified(sgpDetails);
                    _detailsCache.Set(gameId, unified);
                    return unified;
                }
            }
            catch
            {
                // 忽略并降级
            }

        // 2. LCU 本地兜底
        try
        {
            var lcuTimeline = await _lcuApi.GetGameTimelineAsync(gameId, cancellationToken).ConfigureAwait(false);
            if (lcuTimeline != null)
            {
                var unified = MatchDataAdapter.LcuTimelineToUnified(gameId, lcuTimeline);
                _detailsCache.Set(gameId, unified);
                return unified;
            }
        }
        catch
        {
            // 返回 null
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _sgpClient.Dispose();
        _summaryCache.Clear();
        _detailsCache.Clear();
    }

    private async Task<T?> ExecuteWithAccessTokenRetryAsync<T>(
        Func<string, Task<T?>> apiCall,
        CancellationToken cancellationToken) where T : class
    {
        if (string.IsNullOrWhiteSpace(_accessToken))
            await RefreshTokensAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(_accessToken))
            return null;

        try
        {
            return await apiCall(_accessToken).ConfigureAwait(false);
        }
        catch (SgpAuthenticationException)
        {
            try
            {
                _accessToken = null;
                if (await RefreshTokensAsync(cancellationToken).ConfigureAwait(false) &&
                    !string.IsNullOrWhiteSpace(_accessToken))
                    return await apiCall(_accessToken).ConfigureAwait(false);
            }
            catch
            {
                _sgpUnavailableUntil = DateTime.UtcNow.AddSeconds(15);
            }

            return null;
        }
        catch (Exception)
        {
            _sgpUnavailableUntil = DateTime.UtcNow.AddSeconds(15);
            return null;
        }
    }

    private async Task<T?> ExecuteWithSessionTokenRetryAsync<T>(
        Func<string, Task<T?>> apiCall,
        CancellationToken cancellationToken) where T : class
    {
        if (string.IsNullOrWhiteSpace(_sessionToken))
            await RefreshTokensAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(_sessionToken))
            return null;

        try
        {
            return await apiCall(_sessionToken).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                _sessionToken = null;
                if (await RefreshTokensAsync(cancellationToken).ConfigureAwait(false) &&
                    !string.IsNullOrWhiteSpace(_sessionToken))
                    return await apiCall(_sessionToken).ConfigureAwait(false);
            }
            catch
            {
            }

            return null;
        }
    }

    private string ResolveEffectivePlatform(string? platformId)
    {
        if (!string.IsNullOrWhiteSpace(platformId))
            return TencentSgpServerConfig.NormalizePlatformId(platformId);

        return _activePlatformId ?? _defaultPlatformId;
    }
}