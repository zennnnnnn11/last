using last.Core.State.Models;

namespace last.Core.GameData.Static;

public interface IChampionStaticDataService : IDisposable
{
    ChampionStaticInfo? GetChampion(int championId);
    string GetChampionName(int championId);
    string GetIconUri(int championId);
    IReadOnlyList<ChampionStaticInfo> GetAllChampions();
    IReadOnlyDictionary<int, IReadOnlyList<string>> GetRolesMap();
    IReadOnlyList<ChampionStaticInfo> Search(string query);

    void UpdateFromLcu(IReadOnlyList<ChampionSimple> lcuChampions);
    void UpdateFromGtimg(GtimgHeroList gtimgData);
    Task InitializeAsync(CancellationToken cancellationToken = default);
}