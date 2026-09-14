namespace last.Core.State.Api;

public sealed class LobbyApi : ILobbyApi
{
    private readonly ILcuRestClient _client;

    public LobbyApi(ILcuRestClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public Task<bool> PlayAgainAsync(CancellationToken cancellationToken = default)
    {
        return _client.PostAsync("/lol-lobby/v2/play-again", null, cancellationToken);
    }

    public Task<bool> SearchMatchAsync(CancellationToken cancellationToken = default)
    {
        return _client.PostAsync("/lol-lobby/v2/lobby/matchmaking/search", null, cancellationToken);
    }
}