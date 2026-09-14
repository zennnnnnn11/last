using last.Core.State.Models;

namespace last.Core.State.Api;

public sealed class GameflowApi : IGameflowApi
{
    public const string ProcessQuitUri = "/process-control/v1/process/quit";

    private readonly ILcuRestClient _client;

    public GameflowApi(ILcuRestClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public Task<GameflowPhase?> GetPhaseAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<GameflowPhase?>("/lol-gameflow/v1/gameflow-phase", cancellationToken);
    }

    public Task<GameflowSession?> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<GameflowSession?>("/lol-gameflow/v1/session", cancellationToken);
    }

    public Task<bool> QuitProcessAsync(CancellationToken cancellationToken = default)
    {
        return _client.PostAsync(ProcessQuitUri, null, cancellationToken);
    }
}