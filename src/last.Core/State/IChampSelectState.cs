using last.Core.State.Models;

namespace last.Core.State;

public interface IChampSelectState
{
    ChampSelectSession? Session { get; }

    int? CurrentChampion { get; }

    IReadOnlyList<BenchChampion>? BenchChampions { get; }

    IReadOnlyList<int>? SubsetChampionList { get; }

    event Action<ChampSelectSession?>? SessionChanged;

    event Action<int?>? CurrentChampionChanged;

    event Action<IReadOnlyList<BenchChampion>?>? BenchChampionsChanged;

    event Action<IReadOnlyList<int>?>? SubsetChampionListChanged;

    void SetSession(ChampSelectSession? session);

    void SetCurrentChampion(int? championId);

    void SetSubsetChampionList(IReadOnlyList<int>? subsetChampionList);

    void Reset();
}