using last.Core.State.Models;

namespace last.Core.State;

public interface IGameflowState
{
    GameflowPhase Phase { get; }

    GameflowSession? Session { get; }

    event Action<GameflowPhase, GameflowPhase>? PhaseChanged;

    event Action<GameflowSession?>? SessionChanged;

    void SetPhase(GameflowPhase? phase);

    void SetSession(GameflowSession? session);

    void Reset();
}