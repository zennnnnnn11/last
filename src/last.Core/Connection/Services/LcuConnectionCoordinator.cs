using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using last.Core.Automation;
using last.Core.Connection.Events;
using last.Core.Connection.Models;
using last.Core.Connection.WebSocket;
using last.Core.GameData.Balance;
using last.Core.GameData.Static;
using last.Core.MatchHistory.Api;
using last.Core.MatchHistory.Services;
using last.Core.State;
using last.Core.State.Api;
using last.Core.State.Models;
using last.Core.State.Services;

namespace last.Core.Connection.Services;

public sealed class LcuConnectionCoordinator : ILcuConnectionCoordinator
{
    private const int MaxFastReconnectAttempts = 5;
    private const int MaxTotalReconnectAttempts = 10;
    private static readonly TimeSpan InitialReconnectDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(30);
    private readonly Lock _reconnectLock = new();

    private readonly ILcuRestClient _restClient;
    private bool _disposed;
    private CancellationTokenSource? _reconnectCts;

    public LcuConnectionCoordinator(
        ILeagueClientDetector? detector = null,
        ILcuEventBus? eventBus = null,
        ILcuWebSocketClient? webSocketClient = null,
        ILcuRestClient? restClient = null,
        ILcuStateCoordinator? stateCoordinator = null,
        IChampionStaticDataService? championStaticData = null,
        IAramBalanceService? aramBalance = null,
        IMatchHistoryService? matchHistory = null,
        IItemStaticDataService? itemStaticData = null,
        IKiwiAugmentStaticDataService? kiwiAugmentStaticData = null,
        ISummonerSpellStaticDataService? summonerSpellStaticData = null)
    {
        EventBus = eventBus ?? new LcuEventBus();
        WebSocketClient = webSocketClient ?? new LcuWebSocketClient(EventBus);
        Detector = detector ?? new LeagueClientDetector();
        _restClient = restClient ?? new LcuRestClient();
        StateCoordinator = stateCoordinator ?? new LcuStateCoordinator(_restClient, EventBus);
        ChampionStaticData = championStaticData ??
                             new ChampionStaticDataService(new GtimgHeroClient(), StateCoordinator.GameDataApi);
        ItemStaticData = itemStaticData ?? new ItemStaticDataService(StateCoordinator.GameDataApi);
        KiwiAugmentStaticData = kiwiAugmentStaticData ?? new KiwiAugmentStaticDataService(new GtimgKiwiClient());
        SummonerSpellStaticData = summonerSpellStaticData ?? new SummonerSpellStaticDataService();
        AramBalance = aramBalance ?? new AramBalanceService(new OpggAramBalanceClient());
        MatchHistory = matchHistory ?? new MatchHistoryService(
            new LcuMatchHistoryApi(_restClient),
            new SgpMatchHistoryClient()
        );

        StateCoordinator.GameData.ChampionsChanged += OnChampionsChanged;
        Detector.CredentialsChanged += OnDetectorCredentialsChanged;
        Detector.StatusChanged += OnDetectorStatusChanged;
        WebSocketClient.Disconnected += OnWebSocketDisconnected;
        _restClient.RequestErrorOccurred += OnRestRequestErrorOccurred;
    }

    public ILeagueClientDetector Detector { get; }

    public ILcuEventBus EventBus { get; }

    public ILcuWebSocketClient WebSocketClient { get; }

    public ILcuStateCoordinator StateCoordinator { get; }

    public IGameflowState Gameflow => StateCoordinator.Gameflow;

    public ISummonerState Summoner => StateCoordinator.Summoner;

    public IMatchmakingState Matchmaking => StateCoordinator.Matchmaking;

    public IChampSelectState ChampSelect => StateCoordinator.ChampSelect;

    public IGameDataState GameData => StateCoordinator.GameData;

    public IChampionStaticDataService ChampionStaticData { get; }

    public IItemStaticDataService ItemStaticData { get; }

    public IKiwiAugmentStaticDataService KiwiAugmentStaticData { get; }

    public ISummonerSpellStaticDataService SummonerSpellStaticData { get; }

    public IAramBalanceService AramBalance { get; }

    public IAutoAcceptService AutoAccept => StateCoordinator.AutoAccept;

    public IAramBenchSwapService BenchSwap => StateCoordinator.BenchSwap;

    public IAutoPlayAgainService AutoPlayAgain => StateCoordinator.AutoPlayAgain;

    public ILobbyApi LobbyApi => StateCoordinator.LobbyApi;

    public IPreEndOfGameApi PreEndOfGameApi => StateCoordinator.PreEndOfGameApi;

    public IMatchHistoryService MatchHistory { get; }

    public void Start()
    {
        _ = ChampionStaticData.InitializeAsync();
        _ = ItemStaticData.InitializeAsync();
        _ = KiwiAugmentStaticData.InitializeAsync();
        _ = AramBalance.InitializeAsync();
        Detector.Start();
    }

    public void DisconnectManually()
    {
        Detector.DisconnectManually();
        _ = Task.Run(async () =>
        {
            try
            {
                await WebSocketClient.DisconnectAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        });
    }

    public void ResumeAutoConnect()
    {
        Detector.ResumeAutoConnect();
        if (!WebSocketClient.IsConnected && Detector.Status == ClientConnectionStatus.Connected &&
            Detector.CurrentCredentials is not null)
            ScheduleWebSocketReconnect();
    }

    public async Task CheckNowAsync()
    {
        await Detector.CheckNowAsync().ConfigureAwait(false);
        if (Detector.Status == ClientConnectionStatus.Connected && Detector.CurrentCredentials is not null)
        {
            if (!WebSocketClient.IsConnected)
                ScheduleWebSocketReconnect();

            if (StateCoordinator.Summoner.Me is null)
                try
                {
                    await StateCoordinator.InitializeAsync().ConfigureAwait(false);
                    _ = ItemStaticData.InitializeAsync();
                    _ = ChampionStaticData.InitializeAsync();
                }
                catch
                {
                }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CancelScheduledReconnect();
        StateCoordinator.GameData.ChampionsChanged -= OnChampionsChanged;
        Detector.CredentialsChanged -= OnDetectorCredentialsChanged;
        Detector.StatusChanged -= OnDetectorStatusChanged;
        WebSocketClient.Disconnected -= OnWebSocketDisconnected;
        _restClient.RequestErrorOccurred -= OnRestRequestErrorOccurred;

        if (AramBalance is IDisposable balanceDisposable)
            balanceDisposable.Dispose();

        ChampionStaticData.Dispose();
        ItemStaticData.Dispose();
        KiwiAugmentStaticData.Dispose();
        MatchHistory.Dispose();

        StateCoordinator.Dispose();
        WebSocketClient.Dispose();
        Detector.Dispose();
        EventBus.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        CancelScheduledReconnect();
        StateCoordinator.GameData.ChampionsChanged -= OnChampionsChanged;
        Detector.CredentialsChanged -= OnDetectorCredentialsChanged;
        Detector.StatusChanged -= OnDetectorStatusChanged;
        WebSocketClient.Disconnected -= OnWebSocketDisconnected;
        _restClient.RequestErrorOccurred -= OnRestRequestErrorOccurred;

        if (AramBalance is IDisposable balanceDisposable)
            balanceDisposable.Dispose();

        ChampionStaticData.Dispose();
        ItemStaticData.Dispose();
        KiwiAugmentStaticData.Dispose();
        MatchHistory.Dispose();

        StateCoordinator.Dispose();
        await WebSocketClient.DisposeAsync().ConfigureAwait(false);
        Detector.Dispose();
        EventBus.Clear();
    }

    private void CancelScheduledReconnect()
    {
        lock (_reconnectLock)
        {
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _reconnectCts = null;
        }
    }

    private void ScheduleWebSocketReconnect()
    {
        if (_disposed)
            return;

        lock (_reconnectLock)
        {
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _reconnectCts = new CancellationTokenSource();
            var token = _reconnectCts.Token;

            _ = Task.Run(async () =>
            {
                var delay = InitialReconnectDelay;
                for (var attempt = 1;
                     attempt <= MaxTotalReconnectAttempts && !token.IsCancellationRequested && !_disposed;
                     attempt++)
                {
                    try
                    {
                        await Task.Delay(delay, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    if (WebSocketClient.IsConnected || _disposed || token.IsCancellationRequested)
                        return;

                    var creds = Detector.CurrentCredentials;
                    if (creds == null || Detector.Status != ClientConnectionStatus.Connected)
                        return;

                    if (attempt == MaxFastReconnectAttempts)
                        try
                        {
                            await Detector.CheckNowAsync(token).ConfigureAwait(false);
                            if (Detector.Status != ClientConnectionStatus.Connected ||
                                Detector.CurrentCredentials is null)
                                return;
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch
                        {
                        }

                    try
                    {
                        _restClient.Configure(creds);
                        await WebSocketClient.ConnectAsync(creds, token).ConfigureAwait(false);
                        if (WebSocketClient.IsConnected)
                        {
                            await StateCoordinator.InitializeAsync().ConfigureAwait(false);
                            _ = ItemStaticData.InitializeAsync();
                            _ = ChampionStaticData.InitializeAsync();
                            return;
                        }
                    }
                    catch
                    {
                        var nextSeconds = Math.Min(MaxReconnectDelay.TotalSeconds, delay.TotalSeconds * 1.5);
                        delay = TimeSpan.FromSeconds(nextSeconds);
                    }
                }
            }, token);
        }
    }

    private void OnDetectorCredentialsChanged(LcuCredentials? credentials)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (credentials is not null)
                {
                    CancelScheduledReconnect();
                    MatchHistory.ActivePlatformId = credentials.PlatformId;
                    _restClient.Configure(credentials);

                    var wsTask = Task.Run(async () =>
                    {
                        try
                        {
                            await WebSocketClient.ConnectAsync(credentials).ConfigureAwait(false);
                        }
                        catch
                        {
                            ScheduleWebSocketReconnect();
                        }
                    });

                    var initStateTask = Task.Run(async () =>
                    {
                        try
                        {
                            await StateCoordinator.InitializeAsync().ConfigureAwait(false);
                            _ = ItemStaticData.InitializeAsync();
                            _ = ChampionStaticData.InitializeAsync();
                        }
                        catch
                        {
                        }
                    });

                    await Task.WhenAll(wsTask, initStateTask).ConfigureAwait(false);
                }
                else
                {
                    CancelScheduledReconnect();
                    MatchHistory.ActivePlatformId = null;
                    StateCoordinator.Reset();
                    _restClient.Reset();
                    await WebSocketClient.DisconnectAsync().ConfigureAwait(false);
                }
            }
            catch
            {
            }
        });
    }

    private void OnDetectorStatusChanged(ClientConnectionStatus status)
    {
        if (status != ClientConnectionStatus.Connected)
        {
            CancelScheduledReconnect();
            MatchHistory.ActivePlatformId = null;
            StateCoordinator.Reset();
            _restClient.Reset();
            if (WebSocketClient.IsConnected)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await WebSocketClient.DisconnectAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                });
        }
    }

    private void OnWebSocketDisconnected(WebSocketCloseStatus? status, string? description)
    {
        if (!_disposed && Detector.Status == ClientConnectionStatus.Connected &&
            Detector.CurrentCredentials is not null)
            ScheduleWebSocketReconnect();
    }

    private void OnChampionsChanged(IReadOnlyList<ChampionSimple> champions)
    {
        if (champions.Count > 0)
            ChampionStaticData.UpdateFromLcu(champions);
    }

    private void OnRestRequestErrorOccurred(string uri, Exception ex)
    {
        if (ex is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized })
        {
            Trace.WriteLine(
                $"[LcuConnectionCoordinator] REST 401 Unauthorized for '{uri}'. Triggering credential re-check.");
            _ = Detector.CheckNowAsync();
        }
    }
}