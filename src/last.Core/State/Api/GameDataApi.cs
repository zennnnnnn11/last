using last.Core.GameData.Static;
using last.Core.State.Models;

namespace last.Core.State.Api;

public sealed class GameDataApi : IGameDataApi
{
    private readonly ILcuRestClient _restClient;

    public GameDataApi(ILcuRestClient restClient)
    {
        _restClient = restClient ?? throw new ArgumentNullException(nameof(restClient));
    }

    public async Task<IReadOnlyList<ChampionSimple>> GetChampionSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await _restClient.GetAsync<IReadOnlyList<ChampionSimple>>(
            "/lol-game-data/assets/v1/champion-summary.json",
            cancellationToken).ConfigureAwait(false);

        return result ?? [];
    }

    public async Task<IReadOnlyList<LcuItemDto>> GetItemsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await _restClient.GetAsync<IReadOnlyList<LcuItemDto>>(
            "/lol-game-data/assets/v1/items.json",
            cancellationToken).ConfigureAwait(false);

        return result ?? [];
    }
}