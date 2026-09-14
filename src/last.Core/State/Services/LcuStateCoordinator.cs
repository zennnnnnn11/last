using System.Text.Json;
using last.Core.Automation;
using last.Core.Connection.Events;
using last.Core.Connection.WebSocket;
using last.Core.State.Api;
using last.Core.State.Models;

namespace last.Core.State.Services;

public sealed class LcuStateCoordinator : ILcuStateCoordinator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = LcuJsonSerializerContext.Default
    };

    private readonly IDisposable _champSelectSessionSubscription;
    private readonly IDisposable _currentChampionSubscription;
    private readonly IDisposable _currentSummonerSubscription;

    private readonly IDisposable _phaseSubscription;
    private readonly IDisposable _profileSubscription;
    private readonly IDisposable _readyCheckSubscription;
    private readonly IDisposable _searchSubscription;

    private readonly IDisposable _sessionSubscription;
    private readonly IDisposable _subsetChampionSubscription;

    private bool _disposed;

    public LcuStateCoordinator(
        ILcuRestClient restClient,
        ILcuEventBus eventBus,
        IGameflowApi? gameflowApi = null,
        ISummonerApi? summonerApi = null,
        IMatchmakingApi? matchmakingApi = null,
        IChampSelectApi? champSelectApi = null,
        IGameDataApi? gameDataApi = null,
        IGameflowState? gameflowState = null,
        ISummonerState? summonerState = null,
        IMatchmakingState? matchmakingState = null,
        IChampSelectState? champSelectState = null,
        IGameDataState? gameDataState = null,
        IAutoAcceptService? autoAccept = null,
        IAramBenchSwapService? benchSwap = null,
        ILobbyApi? lobbyApi = null,
        IPreEndOfGameApi? preEndOfGameApi = null,
        IAutoPlayAgainService? autoPlayAgain = null)
    {
        RestClient = restClient ?? throw new ArgumentNullException(nameof(restClient));
        ArgumentNullException.ThrowIfNull(eventBus);

        Gameflow = gameflowState ?? new GameflowState();
        Summoner = summonerState ?? new SummonerState();
        Matchmaking = matchmakingState ?? new MatchmakingState();
        ChampSelect = champSelectState ?? new ChampSelectState();
        GameData = gameDataState ?? new GameDataState();

        GameflowApi = gameflowApi ?? new GameflowApi(RestClient);
        SummonerApi = summonerApi ?? new SummonerApi(RestClient);
        MatchmakingApi = matchmakingApi ?? new MatchmakingApi(RestClient);
        ChampSelectApi = champSelectApi ?? new ChampSelectApi(RestClient);
        GameDataApi = gameDataApi ?? new GameDataApi(RestClient);
        LobbyApi = lobbyApi ?? new LobbyApi(RestClient);
        PreEndOfGameApi = preEndOfGameApi ?? new PreEndOfGameApi(RestClient);

        AutoAccept = autoAccept ?? new AutoAcceptService(MatchmakingApi, Gameflow, Matchmaking);
        BenchSwap = benchSwap ?? new AramBenchSwapService(ChampSelectApi, Gameflow, ChampSelect);
        AutoPlayAgain = autoPlayAgain ?? new AutoPlayAgainService(LobbyApi, PreEndOfGameApi, Gameflow, eventBus);

        _phaseSubscription = eventBus.Subscribe("/lol-gameflow/v1/gameflow-phase", OnGameflowPhaseEvent);
        _sessionSubscription = eventBus.Subscribe("/lol-gameflow/v1/session", OnGameflowSessionEvent);
        _currentSummonerSubscription = eventBus.Subscribe("/lol-summoner/v1/current-summoner", OnCurrentSummonerEvent);
        _profileSubscription =
            eventBus.Subscribe("/lol-summoner/v1/current-summoner/summoner-profile", OnSummonerProfileEvent);
        _readyCheckSubscription = eventBus.Subscribe("/lol-matchmaking/v1/ready-check", OnReadyCheckEvent);
        _searchSubscription = eventBus.Subscribe("/lol-matchmaking/v1/search", OnSearchEvent);
        _champSelectSessionSubscription = eventBus.Subscribe("/lol-champ-select/v1/session", OnChampSelectSessionEvent);
        _currentChampionSubscription =
            eventBus.Subscribe("/lol-champ-select/v1/current-champion", OnCurrentChampionEvent);
        _subsetChampionSubscription = eventBus.Subscribe("/lol-lobby-team-builder/champ-select/v1/subset-champion-list",
            OnSubsetChampionListEvent);
    }

    public IGameflowState Gameflow { get; }

    public ISummonerState Summoner { get; }

    public IMatchmakingState Matchmaking { get; }

    public IChampSelectState ChampSelect { get; }

    public IGameDataState GameData { get; }

    public IAutoAcceptService AutoAccept { get; }

    public IAramBenchSwapService BenchSwap { get; }

    public IAutoPlayAgainService AutoPlayAgain { get; }

    public ILobbyApi LobbyApi { get; }

    public IPreEndOfGameApi PreEndOfGameApi { get; }

    public IGameflowApi GameflowApi { get; }

    public ISummonerApi SummonerApi { get; }

    public IMatchmakingApi MatchmakingApi { get; }

    public IChampSelectApi ChampSelectApi { get; }

    public IGameDataApi GameDataApi { get; }

    public ILcuRestClient RestClient { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!RestClient.IsConfigured)
            return;

        var phaseTask = GameflowApi.GetPhaseAsync(cancellationToken);
        var sessionTask = GameflowApi.GetSessionAsync(cancellationToken);
        var summonerTask = SummonerApi.GetCurrentSummonerAsync(cancellationToken);
        var readyCheckTask = MatchmakingApi.GetReadyCheckAsync(cancellationToken);
        var searchTask = MatchmakingApi.GetSearchAsync(cancellationToken);
        var champSelectSessionTask = ChampSelectApi.GetSessionAsync(cancellationToken);
        var subsetChampionTask = ChampSelectApi.GetSubsetChampionListAsync(cancellationToken);
        var championsTask = GameDataApi.GetChampionSummaryAsync(cancellationToken);

        await Task.WhenAll(
            SafeLoadAsync(phaseTask, phase => Gameflow.SetPhase(phase)),
            SafeLoadAsync(sessionTask, session => Gameflow.SetSession(session)),
            SafeLoadAsync(readyCheckTask, rc => Matchmaking.SetReadyCheck(rc)),
            SafeLoadAsync(searchTask, s => Matchmaking.SetSearch(s)),
            SafeLoadAsync(champSelectSessionTask, cs => ChampSelect.SetSession(cs)),
            SafeLoadAsync(subsetChampionTask, list => ChampSelect.SetSubsetChampionList(list)),
            SafeLoadAsync(championsTask, champions =>
            {
                if (champions is { Count: > 0 })
                    GameData.UpdateChampions(champions);
            }),
            SafeLoadAsync(summonerTask, summoner => Summoner.SetMe(summoner))
        ).ConfigureAwait(false);
    }

    public void Reset()
    {
        Gameflow.Reset();
        Summoner.Reset();
        Matchmaking.Reset();
        ChampSelect.Reset();
        GameData.Reset();
        AutoAccept.Cancel("reset");
        BenchSwap.Cancel("reset");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _phaseSubscription.Dispose();
        _sessionSubscription.Dispose();
        _currentSummonerSubscription.Dispose();
        _profileSubscription.Dispose();
        _readyCheckSubscription.Dispose();
        _searchSubscription.Dispose();
        _champSelectSessionSubscription.Dispose();
        _currentChampionSubscription.Dispose();
        _subsetChampionSubscription.Dispose();
        AutoAccept.Dispose();
        BenchSwap.Dispose();
        AutoPlayAgain.Dispose();
        RestClient.Dispose();
    }

    private void OnGameflowPhaseEvent(LcuEvent evt)
    {
        if (evt.EventType.Equals("Delete", StringComparison.OrdinalIgnoreCase))
        {
            Gameflow.SetPhase(GameflowPhase.None);
            ChampSelect.SetSubsetChampionList(null);
            return;
        }

        var phase = evt.GetData<GameflowPhase>(JsonOptions);
        Gameflow.SetPhase(phase);

        if (phase == GameflowPhase.ChampSelect)
            _ = Task.Run(async () =>
            {
                try
                {
                    var list = await ChampSelectApi.GetSubsetChampionListAsync().ConfigureAwait(false);
                    ChampSelect.SetSubsetChampionList(list);
                }
                catch
                {
                }
            });
        else
            ChampSelect.SetSubsetChampionList(null);
    }

    private void OnGameflowSessionEvent(LcuEvent evt)
    {
        if (evt.EventType.Equals("Delete", StringComparison.OrdinalIgnoreCase))
        {
            Gameflow.SetSession(null);
            return;
        }

        var session = evt.GetData<GameflowSession>(JsonOptions);
        Gameflow.SetSession(session);
    }

    private void OnCurrentSummonerEvent(LcuEvent evt)
    {
        if (evt.EventType.Equals("Delete", StringComparison.OrdinalIgnoreCase))
        {
            Summoner.SetMe(null);
            return;
        }

        var summoner = evt.GetData<SummonerInfo>(JsonOptions);
        Summoner.SetMe(summoner);
    }

    private void OnSummonerProfileEvent(LcuEvent evt)
    {
        if (evt.EventType.Equals("Delete", StringComparison.OrdinalIgnoreCase))
        {
            Summoner.SetProfile(null);
            return;
        }

        var profile = evt.GetData<SummonerProfile>(JsonOptions);
        Summoner.SetProfile(profile);
    }

    private void OnSearchEvent(LcuEvent evt)
    {
        if (evt.EventType.Equals("Delete", StringComparison.OrdinalIgnoreCase))
        {
            Matchmaking.SetSearch(null);
            return;
        }

        var search = evt.GetData<MatchmakingSearch>(JsonOptions);
        Matchmaking.SetSearch(search);
    }

    private void OnReadyCheckEvent(LcuEvent evt)
    {
        if (evt.EventType.Equals("Delete", StringComparison.OrdinalIgnoreCase))
        {
            Matchmaking.SetReadyCheck(null);
            return;
        }

        var readyCheck = evt.GetData<ReadyCheck>(JsonOptions);
        Matchmaking.SetReadyCheck(readyCheck);
    }

    private void OnChampSelectSessionEvent(LcuEvent evt)
    {
        if (evt.EventType.Equals("Delete", StringComparison.OrdinalIgnoreCase))
        {
            ChampSelect.SetSession(null);
            return;
        }

        var session = evt.GetData<ChampSelectSession>(JsonOptions);
        ChampSelect.SetSession(session);
    }

    private void OnCurrentChampionEvent(LcuEvent evt)
    {
        if (evt.EventType.Equals("Delete", StringComparison.OrdinalIgnoreCase))
        {
            ChampSelect.SetCurrentChampion(null);
            return;
        }

        var champId = evt.GetData<int>(JsonOptions);
        ChampSelect.SetCurrentChampion(champId);
    }

    private void OnSubsetChampionListEvent(LcuEvent evt)
    {
        if (evt.EventType.Equals("Delete", StringComparison.OrdinalIgnoreCase) ||
            evt.Data.ValueKind == JsonValueKind.Null)
        {
            ChampSelect.SetSubsetChampionList(null);
            return;
        }

        try
        {
            var list = evt.GetData<IReadOnlyList<int>>(JsonOptions);
            ChampSelect.SetSubsetChampionList(list);
        }
        catch
        {
        }
    }

    private static async Task SafeLoadAsync<T>(Task<T> task, Action<T> apply)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            apply(result);
        }
        catch
        {
        }
    }
}