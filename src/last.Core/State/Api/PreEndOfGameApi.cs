namespace last.Core.State.Api;

public sealed class PreEndOfGameApi : IPreEndOfGameApi
{
    private readonly ILcuRestClient _client;

    public PreEndOfGameApi(ILcuRestClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public Task<bool> CompleteSequenceEventAsync(string sequenceEventName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sequenceEventName);
        return _client.PostAsync($"/lol-pre-end-of-game/v1/complete/{sequenceEventName}", null, cancellationToken);
    }
}