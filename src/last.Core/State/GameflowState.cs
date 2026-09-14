using last.Core.State.Models;

namespace last.Core.State;

public sealed class GameflowState : IGameflowState
{
    private readonly Lock _lock = new();
    private GameflowPhase _phase = GameflowPhase.None;
    private GameflowSession? _session;

    public GameflowPhase Phase
    {
        get
        {
            lock (_lock)
            {
                return _phase;
            }
        }
    }

    public GameflowSession? Session
    {
        get
        {
            lock (_lock)
            {
                return _session;
            }
        }
    }

    public event Action<GameflowPhase, GameflowPhase>? PhaseChanged;

    public event Action<GameflowSession?>? SessionChanged;

    public void SetPhase(GameflowPhase? phase)
    {
        var targetPhase = phase ?? GameflowPhase.None;
        GameflowPhase oldPhase;
        bool changed;

        lock (_lock)
        {
            oldPhase = _phase;
            if (oldPhase == targetPhase)
                return;

            _phase = targetPhase;
            changed = true;
        }

        if (changed)
            PhaseChanged?.Invoke(oldPhase, targetPhase);
    }

    public void SetSession(GameflowSession? session)
    {
        bool changed;

        lock (_lock)
        {
            if (ReferenceEquals(_session, session) || (_session is not null && _session.Equals(session)))
                return;

            _session = session;
            changed = true;
        }

        if (changed)
            SessionChanged?.Invoke(session);
    }

    public void Reset()
    {
        GameflowPhase oldPhase;
        var phaseChanged = false;
        var sessionChanged = false;

        lock (_lock)
        {
            oldPhase = _phase;
            if (_phase != GameflowPhase.None)
            {
                _phase = GameflowPhase.None;
                phaseChanged = true;
            }

            if (_session is not null)
            {
                _session = null;
                sessionChanged = true;
            }
        }

        if (phaseChanged)
            PhaseChanged?.Invoke(oldPhase, GameflowPhase.None);

        if (sessionChanged)
            SessionChanged?.Invoke(null);
    }
}