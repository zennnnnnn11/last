namespace last.Core.GameData.Balance;

public sealed class AramBalanceService : IAramBalanceService, IDisposable
{
    public static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromMinutes(30);

    private readonly IOpggAramBalanceClient _balanceClient;
    private readonly Lock _lock = new();
    private readonly TimeSpan _refreshInterval;

    private Dictionary<int, AramChampionBalance> _balances = [];
    private bool _disposed;
    private CancellationTokenSource? _timerCts;
    private Task? _timerTask;

    public AramBalanceService(
        IOpggAramBalanceClient balanceClient,
        TimeSpan? refreshInterval = null)
    {
        _balanceClient = balanceClient ?? throw new ArgumentNullException(nameof(balanceClient));
        _refreshInterval = refreshInterval ?? DefaultRefreshInterval;
    }

    public AramChampionBalance GetBalance(int championId)
    {
        lock (_lock)
        {
            return _balances.GetValueOrDefault(championId, AramChampionBalance.Neutral);
        }
    }

    public bool HasBalance(int championId)
    {
        lock (_lock)
        {
            return _balances.TryGetValue(championId, out var balance) && balance.Adjustments.Count > 0;
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _balanceClient.GetAramBalanceAsync(cancellationToken).ConfigureAwait(false);
            if (items is null or { Count: 0 })
                return;

            var newMap = new Dictionary<int, AramChampionBalance>(items.Count);
            foreach (var item in items)
            {
                var calculated = AramBalanceCalculator.Calculate(item);
                newMap[item.ChampionId] = calculated;
            }

            lock (_lock)
            {
                _balances = newMap;
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken).ConfigureAwait(false);

        lock (_lock)
        {
            if (_disposed || _timerTask is not null)
                return;

            _timerCts = new CancellationTokenSource();
            _timerTask = RunPeriodicRefreshLoopAsync(_timerCts.Token);
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
            ctsToDispose = _timerCts;
            _timerCts = null;
        }

        ctsToDispose?.Cancel();
        ctsToDispose?.Dispose();
        (_balanceClient as IDisposable)?.Dispose();
    }

    private async Task RunPeriodicRefreshLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_refreshInterval);

        while (!cancellationToken.IsCancellationRequested)
            try
            {
                if (!await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                    break;

                await RefreshAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
            }
    }
}