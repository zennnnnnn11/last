using last.Core.State.Models;

namespace last.Core.State;

public interface IGameDataState
{
    IReadOnlyList<ChampionSimple> Champions { get; }
    ChampionSimple? GetChampion(int championId);
    string GetChampionName(int championId);
    event Action<IReadOnlyList<ChampionSimple>>? ChampionsChanged;
    void UpdateChampions(IReadOnlyList<ChampionSimple> champions);
    void Reset();
}