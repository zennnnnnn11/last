using last.Core.State.Models;

namespace last.Core.State.Api;

public sealed class ChampSelectApi : IChampSelectApi
{
    private readonly ILcuRestClient _client;

    public ChampSelectApi(ILcuRestClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public Task<ChampSelectSession?> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<ChampSelectSession?>("/lol-champ-select/v1/session", cancellationToken);
    }

    public Task<bool> BenchSwapAsync(int championId, CancellationToken cancellationToken = default)
    {
        return _client.PostAsync($"/lol-champ-select/v1/session/bench/swap/{championId}", null, cancellationToken);
    }

    public Task<bool> ActionAsync(
        long actionId,
        int? championId = null,
        bool? completed = null,
        string? type = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new ChampSelectActionUpdateRequest(championId, completed, type);
        return _client.PatchAsync($"/lol-champ-select/v1/session/actions/{actionId}", payload, cancellationToken);
    }

    public Task<IReadOnlyList<int>?> GetSubsetChampionListAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<IReadOnlyList<int>>("/lol-lobby-team-builder/champ-select/v1/subset-champion-list",
            cancellationToken);
    }
}