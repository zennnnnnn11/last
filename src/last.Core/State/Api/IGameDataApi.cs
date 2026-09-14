using last.Core.GameData.Static;
using last.Core.State.Models;

namespace last.Core.State.Api;

public interface IGameDataApi
{
    Task<IReadOnlyList<ChampionSimple>> GetChampionSummaryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LcuItemDto>> GetItemsAsync(CancellationToken cancellationToken = default);
}