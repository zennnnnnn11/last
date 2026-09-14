using last.Core.Connection.Events;
using last.Core.State;
using last.Core.State.Api;
using last.Core.State.Models;

namespace last.Core.Automation;

public sealed class AutoPlayAgainService : IAutoPlayAgainService
{
    private readonly IGameflowState _gameflowState;
    private readonly ILobbyApi _lobbyApi;

    private readonly Lock _lock = new();
    private readonly IPreEndOfGameApi _preEndOfGameApi;
    private readonly IDisposable? _sequenceSubscription;
    private bool _disposed;
    private CancellationTokenSource? _executingCts;
    private CancellationTokenSource? _scheduledCts;
    private AutoPlayAgainSettings _settings;
    private long _willExecuteAt = -1;

    public AutoPlayAgainService(
        ILobbyApi lobbyApi,
        IPreEndOfGameApi preEndOfGameApi,
        IGameflowState gameflowState,
        ILcuEventBus? eventBus = null,
        AutoPlayAgainSettings? settings = null)
    {
        _lobbyApi = lobbyApi ?? throw new ArgumentNullException(nameof(lobbyApi));
        _preEndOfGameApi = preEndOfGameApi ?? throw new ArgumentNullException(nameof(preEndOfGameApi));
        _gameflowState = gameflowState ?? throw new ArgumentNullException(nameof(gameflowState));
        _settings = settings ?? new AutoPlayAgainSettings();

        _gameflowState.PhaseChanged += OnGameflowPhaseChanged;

        if (eventBus is not null)
            _sequenceSubscription = eventBus.Subscribe<SequenceEvent>(
                "/lol-pre-end-of-game/v1/currentSequenceEvent",
                OnSequenceEvent);

        if (_settings.Enabled)
            EvaluateAndSchedule(_gameflowState.Phase);
    }

    public bool IsEnabled
    {
        get
        {
            lock (_lock)
            {
                return _settings.Enabled;
            }
        }
        set => UpdateSettings(_settings with { Enabled = value });
    }

    public AutoPlayAgainSettings Settings
    {
        get
        {
            lock (_lock)
            {
                return _settings;
            }
        }
    }

    public long WillExecuteAt
    {
        get
        {
            lock (_lock)
            {
                return _willExecuteAt;
            }
        }
    }

    public bool IsScheduled
    {
        get
        {
            lock (_lock)
            {
                return _willExecuteAt > 0;
            }
        }
    }

    public event Action<AutoPlayAgainSettings>? SettingsChanged;

    public event Action<long>? Scheduled;

    public event Action<string>? Cancelled;

    public event Action<AutoPlayAgainExecutionResult>? Executed;

    public void UpdateSettings(AutoPlayAgainSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        bool wasEnabled;
        bool isEnabled;

        lock (_lock)
        {
            wasEnabled = _settings.Enabled;
            isEnabled = settings.Enabled;
            _settings = settings;
        }

        SettingsChanged?.Invoke(settings);

        if (wasEnabled && !isEnabled)
            Cancel("disabled");
        else if (!wasEnabled && isEnabled)
            EvaluateAndSchedule(_gameflowState.Phase);
    }

    public async Task<bool> TriggerPlayAgainAsync(CancellationToken cancellationToken = default)
    {
        var playAgainSuccess = await _lobbyApi.PlayAgainAsync(cancellationToken).ConfigureAwait(false);
        var matchSearchTriggered = false;

        bool shouldAutoSearch;
        lock (_lock)
        {
            shouldAutoSearch = _settings.AutoSearchMatch;
        }

        if (playAgainSuccess && shouldAutoSearch)
            try
            {
                for (var attempt = 0; attempt < 8 && !matchSearchTriggered; attempt++)
                {
                    await Task.Delay(attempt == 0 ? 800 : 500, cancellationToken).ConfigureAwait(false);

                    if (_gameflowState.Phase == GameflowPhase.Matchmaking ||
                        _gameflowState.Phase == GameflowPhase.ReadyCheck ||
                        _gameflowState.Phase == GameflowPhase.ChampSelect)
                    {
                        matchSearchTriggered = true;
                        break;
                    }

                    if (_gameflowState.Phase == GameflowPhase.InProgress ||
                        _gameflowState.Phase == GameflowPhase.Reconnect)
                        break;

                    matchSearchTriggered = await _lobbyApi.SearchMatchAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }

        return playAgainSuccess;
    }

    public void Cancel(string reason)
    {
        CancellationTokenSource? scheduledToCancel = null;
        CancellationTokenSource? executingToCancel = null;
        bool hadSchedule;

        lock (_lock)
        {
            hadSchedule = _willExecuteAt > 0 || _scheduledCts is not null;
            if (hadSchedule)
            {
                scheduledToCancel = _scheduledCts;
                _scheduledCts = null;
                _willExecuteAt = -1;
            }

            if (!string.Equals(reason, "phase-exit", StringComparison.OrdinalIgnoreCase))
            {
                executingToCancel = _executingCts;
                _executingCts = null;
            }
        }

        if (scheduledToCancel is not null)
            try
            {
                scheduledToCancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

        if (executingToCancel is not null)
            try
            {
                executingToCancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

        if (hadSchedule)
            Cancelled?.Invoke(reason);
    }

    public void Dispose()
    {
        CancellationTokenSource? scheduledToCancel;
        CancellationTokenSource? executingToCancel;

        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            scheduledToCancel = _scheduledCts;
            executingToCancel = _executingCts;
            _scheduledCts = null;
            _executingCts = null;
            _willExecuteAt = -1;
        }

        _gameflowState.PhaseChanged -= OnGameflowPhaseChanged;
        _sequenceSubscription?.Dispose();

        if (scheduledToCancel is not null)
            try
            {
                scheduledToCancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

        if (executingToCancel is not null)
            try
            {
                executingToCancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
    }

    private void OnGameflowPhaseChanged(GameflowPhase oldPhase, GameflowPhase newPhase)
    {
        EvaluateAndSchedule(newPhase);
    }

    private void EvaluateAndSchedule(GameflowPhase phase)
    {
        bool isEnabled;
        AutoPlayAgainSettings settings;

        lock (_lock)
        {
            if (_disposed)
                return;

            isEnabled = _settings.Enabled;
            settings = _settings;
        }

        if (!isEnabled || (phase != GameflowPhase.PreEndOfGame &&
                           phase != GameflowPhase.EndOfGame &&
                           phase != GameflowPhase.WaitingForStats))
        {
            Cancel("phase-exit");
            return;
        }

        var delayMs = phase switch
        {
            GameflowPhase.PreEndOfGame => settings.PreEndOfGameDelayMs,
            GameflowPhase.WaitingForStats => settings.WaitingForStatsDelayMs,
            _ => settings.EndOfGameDelayMs
        };

        ScheduleTask(phase, Math.Max(0, delayMs));
    }

    private void ScheduleTask(GameflowPhase phase, int delayMs)
    {
        CancellationTokenSource newCts;
        CancellationTokenSource? oldCts;
        long willExecuteAt;

        lock (_lock)
        {
            if (_disposed || _executingCts is not null)
                return;

            oldCts = _scheduledCts;
            newCts = new CancellationTokenSource();
            _scheduledCts = newCts;
            willExecuteAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + delayMs;
            _willExecuteAt = willExecuteAt;
        }

        if (oldCts is not null)
            try
            {
                oldCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

        Scheduled?.Invoke(willExecuteAt);

        _ = ExecutePlayAgainTaskAsync(phase, delayMs, newCts);
    }

    private async Task ExecutePlayAgainTaskAsync(GameflowPhase phase, int delayMs, CancellationTokenSource cts)
    {
        using (cts)
        {
            try
            {
                var token = cts.Token;
                if (delayMs > 0)
                    await Task.Delay(delayMs, token).ConfigureAwait(false);

                bool shouldAutoSearch;
                lock (_lock)
                {
                    if (cts.IsCancellationRequested || !ReferenceEquals(_scheduledCts, cts))
                        return;

                    shouldAutoSearch = _settings.AutoSearchMatch;
                    _scheduledCts = null;
                    _willExecuteAt = -1;
                    _executingCts = cts;
                }

                var playAgainSuccess = await _lobbyApi.PlayAgainAsync(token).ConfigureAwait(false);
                var matchSearchTriggered = false;

                if (playAgainSuccess && shouldAutoSearch)
                    try
                    {
                        for (var attempt = 0; attempt < 8 && !matchSearchTriggered; attempt++)
                        {
                            await Task.Delay(attempt == 0 ? 800 : 500, token).ConfigureAwait(false);

                            if (_gameflowState.Phase == GameflowPhase.Matchmaking ||
                                _gameflowState.Phase == GameflowPhase.ReadyCheck ||
                                _gameflowState.Phase == GameflowPhase.ChampSelect)
                            {
                                matchSearchTriggered = true;
                                break;
                            }

                            if (_gameflowState.Phase == GameflowPhase.InProgress ||
                                _gameflowState.Phase == GameflowPhase.Reconnect)
                                break;

                            matchSearchTriggered = await _lobbyApi.SearchMatchAsync(token).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception)
                    {
                    }

                Executed?.Invoke(new AutoPlayAgainExecutionResult(
                    playAgainSuccess,
                    phase,
                    matchSearchTriggered));
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    if (ReferenceEquals(_scheduledCts, cts))
                    {
                        _willExecuteAt = -1;
                        _scheduledCts = null;
                    }
                }

                Executed?.Invoke(new AutoPlayAgainExecutionResult(
                    false,
                    phase,
                    ErrorMessage: ex.Message));
            }
            finally
            {
                lock (_lock)
                {
                    if (ReferenceEquals(_executingCts, cts))
                        _executingCts = null;
                }
            }
        }
    }

    private void OnSequenceEvent(LcuEvent<SequenceEvent> e)
    {
        bool shouldSkip;
        lock (_lock)
        {
            shouldSkip = !_disposed && _settings.Enabled && _settings.AutoSkipCelebrations;
        }

        if (shouldSkip && string.Equals(e.Data?.Name, "missions-celebration", StringComparison.OrdinalIgnoreCase))
            _ = CompleteCelebrationAsync();
    }

    private async Task CompleteCelebrationAsync()
    {
        try
        {
            await _preEndOfGameApi.CompleteSequenceEventAsync("missions-celebration").ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }
}