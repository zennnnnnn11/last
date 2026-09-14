namespace last.Core.State.Api;

public interface IPreEndOfGameApi
{
    Task<bool> CompleteSequenceEventAsync(string sequenceEventName, CancellationToken cancellationToken = default);
}