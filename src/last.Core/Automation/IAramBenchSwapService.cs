namespace last.Core.Automation;

public interface IAramBenchSwapService : IDisposable
{
    Task<bool> SwapAsync(int championId, CancellationToken cancellationToken = default);

    event Action<int, bool>? SwapExecuted;

    void Cancel(string reason = "");
}