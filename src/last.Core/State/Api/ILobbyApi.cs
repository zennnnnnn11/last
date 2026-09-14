namespace last.Core.State.Api;

public interface ILobbyApi
{
    Task<bool> PlayAgainAsync(CancellationToken cancellationToken = default);

    Task<bool> SearchMatchAsync(CancellationToken cancellationToken = default);
}