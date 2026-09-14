using last.Core.State.Models;

namespace last.Core.State.Api;

/// <summary>
///     匹配系统与对局就绪确认 REST API 实现。
/// </summary>
public sealed class MatchmakingApi : IMatchmakingApi
{
    private readonly ILcuRestClient _client;

    public MatchmakingApi(ILcuRestClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public Task<ReadyCheck?> GetReadyCheckAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<ReadyCheck?>("/lol-matchmaking/v1/ready-check", cancellationToken);
    }

    public Task<bool> AcceptReadyCheckAsync(CancellationToken cancellationToken = default)
    {
        return _client.PostAsync("/lol-matchmaking/v1/ready-check/accept", null, cancellationToken);
    }

    public Task<MatchmakingSearch?> GetSearchAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<MatchmakingSearch?>("/lol-matchmaking/v1/search", cancellationToken);
    }
}