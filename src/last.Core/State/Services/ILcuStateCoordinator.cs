using last.Core.Automation;
using last.Core.State.Api;

namespace last.Core.State.Services;

public interface ILcuStateCoordinator : IDisposable
{
    IGameflowState Gameflow { get; }

    ISummonerState Summoner { get; }

    IMatchmakingState Matchmaking { get; }

    IChampSelectState ChampSelect { get; }

    IAutoAcceptService AutoAccept { get; }

    IAramBenchSwapService BenchSwap { get; }

    IAutoPlayAgainService AutoPlayAgain { get; }

    IGameDataState GameData { get; }

    IGameflowApi GameflowApi { get; }

    ILobbyApi LobbyApi { get; }

    IPreEndOfGameApi PreEndOfGameApi { get; }

    ISummonerApi SummonerApi { get; }

    IMatchmakingApi MatchmakingApi { get; }

    IChampSelectApi ChampSelectApi { get; }

    IGameDataApi GameDataApi { get; }

    ILcuRestClient RestClient { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    void Reset();
}