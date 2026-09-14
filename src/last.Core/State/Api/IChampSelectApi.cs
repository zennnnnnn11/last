using last.Core.State.Models;

namespace last.Core.State.Api;

public interface IChampSelectApi
{
    Task<ChampSelectSession?> GetSessionAsync(CancellationToken cancellationToken = default);

    Task<bool> BenchSwapAsync(int championId, CancellationToken cancellationToken = default);

    Task<bool> ActionAsync(long actionId, int? championId = null, bool? completed = null, string? type = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>?> GetSubsetChampionListAsync(CancellationToken cancellationToken = default);
}