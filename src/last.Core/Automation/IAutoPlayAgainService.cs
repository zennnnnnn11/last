namespace last.Core.Automation;

public interface IAutoPlayAgainService : IDisposable
{
    bool IsEnabled { get; set; }

    AutoPlayAgainSettings Settings { get; }

    long WillExecuteAt { get; }

    bool IsScheduled { get; }

    void UpdateSettings(AutoPlayAgainSettings settings);

    Task<bool> TriggerPlayAgainAsync(CancellationToken cancellationToken = default);

    void Cancel(string reason);

    event Action<AutoPlayAgainSettings>? SettingsChanged;

    event Action<long>? Scheduled;

    event Action<string>? Cancelled;

    event Action<AutoPlayAgainExecutionResult>? Executed;
}