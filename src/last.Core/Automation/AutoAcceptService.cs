using last.Core.State;
using last.Core.State.Api;
using last.Core.State.Models;

namespace last.Core.Automation;

public sealed class AutoAcceptService : IAutoAcceptService
{
    private readonly IGameflowState _gameflowState;

    private readonly Lock _lock = new();
    private readonly IMatchmakingApi _matchmakingApi;
    private readonly IMatchmakingState _matchmakingState;
    private double _delaySeconds;
    private bool _disposed;
    private bool _isEnabled;
    private CancellationTokenSource? _scheduledCts;
    private long _willAcceptAt = -1;

    public AutoAcceptService(
        IMatchmakingApi matchmakingApi,
        IGameflowState gameflowState,
        IMatchmakingState matchmakingState,
        bool isEnabled = false,
        double delaySeconds = 0.0)
    {
        _matchmakingApi = matchmakingApi ?? throw new ArgumentNullException(nameof(matchmakingApi));
        _gameflowState = gameflowState ?? throw new ArgumentNullException(nameof(gameflowState));
        _matchmakingState = matchmakingState ?? throw new ArgumentNullException(nameof(matchmakingState));

        _isEnabled = isEnabled;
        _delaySeconds = Math.Max(0.0, delaySeconds);

        _gameflowState.PhaseChanged += OnGameflowPhaseChanged;
        _matchmakingState.ReadyCheckChanged += OnReadyCheckChanged;
    }

    public bool IsEnabled
    {
        get
        {
            lock (_lock)
            {
                return _isEnabled;
            }
        }
        set
        {
            var shouldCancel = false;
            var shouldSchedule = false;
            lock (_lock)
            {
                if (_isEnabled == value)
                    return;

                _isEnabled = value;
                if (!_isEnabled && _willAcceptAt > 0)
                    shouldCancel = true;
                else if (_isEnabled && _gameflowState.Phase == GameflowPhase.ReadyCheck)
                    shouldSchedule = true;
            }

            if (shouldCancel)
                Cancel("disabled");
            else if (shouldSchedule)
                TryScheduleAccept();
        }
    }

    public double DelaySeconds
    {
        get
        {
            lock (_lock)
            {
                return _delaySeconds;
            }
        }
        set
        {
            lock (_lock)
            {
                _delaySeconds = Math.Max(0.0, value);
            }
        }
    }

    public long WillAcceptAt
    {
        get
        {
            lock (_lock)
            {
                return _willAcceptAt;
            }
        }
    }

    public bool IsScheduled
    {
        get
        {
            lock (_lock)
            {
                return _willAcceptAt > 0;
            }
        }
    }

    public event Action<long>? Scheduled;

    public event Action<string>? Cancelled;

    public event Action<bool>? Accepted;

    public void Cancel(string reason)
    {
        CancellationTokenSource? ctsToCancel = null;
        var hadSchedule = _willAcceptAt > 0 || _scheduledCts is not null;

        lock (_lock)
        {
            if (hadSchedule)
            {
                ctsToCancel = _scheduledCts;
                _scheduledCts = null;
                _willAcceptAt = -1;
            }
        }

        if (ctsToCancel is not null)
            try
            {
                ctsToCancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

        if (hadSchedule)
            Cancelled?.Invoke(reason);
    }

    public void Dispose()
    {
        CancellationTokenSource? ctsToDispose;
        bool hadSchedule;

        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            hadSchedule = _willAcceptAt > 0 || _scheduledCts is not null;
            ctsToDispose = _scheduledCts;
            _scheduledCts = null;
            _willAcceptAt = -1;
        }

        _gameflowState.PhaseChanged -= OnGameflowPhaseChanged;
        _matchmakingState.ReadyCheckChanged -= OnReadyCheckChanged;

        if (ctsToDispose is not null)
        {
            try
            {
                ctsToDispose.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            ctsToDispose.Dispose();
        }

        if (hadSchedule)
            Cancelled?.Invoke("disposed");
    }

    private void OnGameflowPhaseChanged(GameflowPhase oldPhase, GameflowPhase newPhase)
    {
        if (newPhase == GameflowPhase.ReadyCheck)
            TryScheduleAccept();
        else
            Cancel("phase_changed");
    }

    private void OnReadyCheckChanged(ReadyCheck? readyCheck)
    {
        if (readyCheck is null)
            return;

        if (readyCheck.IsAccepted || readyCheck.IsDeclined)
            Cancel(readyCheck.PlayerResponse.ToLowerInvariant());
    }

    private void TryScheduleAccept()
    {
        CancellationTokenSource? oldCts = null;
        CancellationTokenSource newCts;
        long targetTimestamp;
        int delayMs;

        lock (_lock)
        {
            if (_disposed || !_isEnabled)
                return;

            if (_scheduledCts is not null)
            {
                oldCts = _scheduledCts;
                _scheduledCts = null;
            }

            delayMs = Math.Max(0, (int)(_delaySeconds * 1000));
            targetTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + delayMs;
            _willAcceptAt = targetTimestamp;

            newCts = new CancellationTokenSource();
            _scheduledCts = newCts;
        }

        if (oldCts is not null)
            try
            {
                oldCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

        Scheduled?.Invoke(targetTimestamp);
        _ = ExecuteAcceptAfterDelayAsync(delayMs, newCts);
    }

    private async Task ExecuteAcceptAfterDelayAsync(int delayMs, CancellationTokenSource cts)
    {
        using (cts)
        {
            try
            {
                if (delayMs > 0)
                    await Task.Delay(delayMs, cts.Token).ConfigureAwait(false);

                lock (_lock)
                {
                    if (cts.IsCancellationRequested || !ReferenceEquals(_scheduledCts, cts))
                        return;

                    _scheduledCts = null;
                    _willAcceptAt = -1;
                }

                bool success;
                try
                {
                    success = await _matchmakingApi.AcceptReadyCheckAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception)
                {
                    success = false;
                }

                Accepted?.Invoke(success);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}