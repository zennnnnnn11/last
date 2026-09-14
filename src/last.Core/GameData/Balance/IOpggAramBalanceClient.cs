namespace last.Core.GameData.Balance;

public interface IOpggAramBalanceClient
{
    Task<IReadOnlyList<OpggAramBalanceItem>?> GetAramBalanceAsync(CancellationToken cancellationToken = default);
}