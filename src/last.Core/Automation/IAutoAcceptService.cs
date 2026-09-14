namespace last.Core.Automation;

public interface IAutoAcceptService : IDisposable
{
    bool IsEnabled { get; set; }

    double DelaySeconds { get; set; }

    long WillAcceptAt { get; }

    bool IsScheduled { get; }

    event Action<long>? Scheduled;

    event Action<string>? Cancelled;

    event Action<bool>? Accepted;

    void Cancel(string reason);
}