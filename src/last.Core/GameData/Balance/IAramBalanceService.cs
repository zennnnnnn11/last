namespace last.Core.GameData.Balance;

public interface IAramBalanceService
{
    AramChampionBalance GetBalance(int championId);
    bool HasBalance(int championId);
    Task RefreshAsync(CancellationToken cancellationToken = default);
    Task InitializeAsync(CancellationToken cancellationToken = default);
}