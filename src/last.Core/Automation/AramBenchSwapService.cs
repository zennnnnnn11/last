using last.Core.State;
using last.Core.State.Api;
using last.Core.State.Models;

namespace last.Core.Automation;

public sealed class AramBenchSwapService : IAramBenchSwapService
{
    private readonly IChampSelectApi _champSelectApi;
    private readonly IChampSelectState _champSelectState;
    private readonly IGameflowState _gameflowState;
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _swapGate = new(1, 1);
    private CancellationTokenSource? _currentSwapCts;
    private volatile bool _disposed;

    public AramBenchSwapService(
        IChampSelectApi champSelectApi,
        IGameflowState gameflowState,
        IChampSelectState champSelectState)
    {
        _champSelectApi = champSelectApi ?? throw new ArgumentNullException(nameof(champSelectApi));
        _gameflowState = gameflowState ?? throw new ArgumentNullException(nameof(gameflowState));
        _champSelectState = champSelectState ?? throw new ArgumentNullException(nameof(champSelectState));
    }

    public event Action<int, bool>? SwapExecuted;

    public async Task<bool> SwapAsync(int championId, CancellationToken cancellationToken = default)
    {
        if (championId <= 0)
            return false;

        var swapCts = new CancellationTokenSource();
        CancellationTokenSource? oldSwapCts = null;
        var shouldReject = false;
        lock (_lock)
        {
            if (_disposed || _gameflowState.Phase != GameflowPhase.ChampSelect)
            {
                shouldReject = true;
            }
            else
            {
                oldSwapCts = _currentSwapCts;
                _currentSwapCts = swapCts;
            }
        }

        if (shouldReject)
        {
            swapCts.Dispose();
            return false;
        }

        if (oldSwapCts is not null)
        {
            try
            {
                await oldSwapCts.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }

            oldSwapCts.Dispose();
        }

        using (swapCts)
        {
            var gateAcquired = false;
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, swapCts.Token);
                var token = linkedCts.Token;

                gateAcquired = await _swapGate.WaitAsync(0, token).ConfigureAwait(false);
                if (!gateAcquired)
                    return false;

                var session = _champSelectState.Session;
                var subset = _champSelectState.SubsetChampionList;

                if (session?.Timer?.Phase == "BAN_PICK" && subset is not null && subset.Contains(championId))
                {
                    var action = session.Actions?
                        .SelectMany(a => a)
                        .FirstOrDefault(a =>
                            a.ActorCellId == session.LocalPlayerCellId && a.Type == "pick" && !a.Completed);

                    if (action is not null)
                    {
                        var actionSuccess = await _champSelectApi
                            .ActionAsync(action.Id, championId, true, "pick", token)
                            .ConfigureAwait(false);
                        SwapExecuted?.Invoke(championId, actionSuccess);
                        return actionSuccess;
                    }
                }

                var benchSuccess = await _champSelectApi.BenchSwapAsync(championId, token).ConfigureAwait(false);
                SwapExecuted?.Invoke(championId, benchSuccess);
                return benchSuccess;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (Exception)
            {
                SwapExecuted?.Invoke(championId, false);
                return false;
            }
            finally
            {
                if (gateAcquired)
                    try
                    {
                        _swapGate.Release();
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                lock (_lock)
                {
                    if (ReferenceEquals(_currentSwapCts, swapCts))
                        _currentSwapCts = null;
                }
            }
        }
    }

    public void Cancel(string reason = "")
    {
        _ = reason;
        CancellationTokenSource? ctsToCancel;
        lock (_lock)
        {
            ctsToCancel = _currentSwapCts;
            _currentSwapCts = null;
        }

        if (ctsToCancel is not null)
            try
            {
                ctsToCancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
    }

    public void Dispose()
    {
        CancellationTokenSource? ctsToDispose;
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            ctsToDispose = _currentSwapCts;
            _currentSwapCts = null;
        }

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

        _swapGate.Dispose();
    }
}