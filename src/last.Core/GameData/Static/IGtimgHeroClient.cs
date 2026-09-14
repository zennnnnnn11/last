namespace last.Core.GameData.Static;

public interface IGtimgHeroClient
{
    Task<GtimgHeroList?> GetHeroListAsync(CancellationToken cancellationToken = default);
}