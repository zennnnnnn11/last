using last.Core.State.Models;

namespace last.Core.State.Api;

public interface IGameflowApi
{
    Task<GameflowPhase?> GetPhaseAsync(CancellationToken cancellationToken = default);

    Task<GameflowSession?> GetSessionAsync(CancellationToken cancellationToken = default);

    Task<bool> QuitProcessAsync(CancellationToken cancellationToken = default);
}