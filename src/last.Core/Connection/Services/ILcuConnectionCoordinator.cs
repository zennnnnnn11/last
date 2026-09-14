using last.Core.Automation;
using last.Core.Connection.Events;
using last.Core.Connection.WebSocket;
using last.Core.GameData.Balance;
using last.Core.GameData.Static;
using last.Core.MatchHistory.Services;
using last.Core.State;
using last.Core.State.Api;
using last.Core.State.Services;

namespace last.Core.Connection.Services;

public interface ILcuConnectionCoordinator : IDisposable, IAsyncDisposable
{
    ILeagueClientDetector Detector { get; }

    ILcuEventBus EventBus { get; }

    ILcuWebSocketClient WebSocketClient { get; }

    ILcuStateCoordinator StateCoordinator { get; }

    IGameflowState Gameflow { get; }

    ISummonerState Summoner { get; }

    IMatchmakingState Matchmaking { get; }

    IChampSelectState ChampSelect { get; }

    IGameDataState GameData { get; }

    IChampionStaticDataService ChampionStaticData { get; }

    IItemStaticDataService ItemStaticData { get; }

    IKiwiAugmentStaticDataService KiwiAugmentStaticData { get; }

    ISummonerSpellStaticDataService SummonerSpellStaticData { get; }

    IAramBalanceService AramBalance { get; }

    IAutoAcceptService AutoAccept { get; }

    IAramBenchSwapService BenchSwap { get; }

    IAutoPlayAgainService AutoPlayAgain { get; }

    ILobbyApi LobbyApi { get; }

    IPreEndOfGameApi PreEndOfGameApi { get; }

    IMatchHistoryService MatchHistory { get; }

    void Start();

    void DisconnectManually();

    void ResumeAutoConnect();

    Task CheckNowAsync();
}