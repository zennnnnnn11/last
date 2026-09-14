using last.Core.Connection.Models;

namespace last.Core.Connection.Services;

public interface ILeagueClientDetector : IDisposable
{
    ClientConnectionStatus Status { get; }

    LcuCredentials? CurrentCredentials { get; }

    ProcessScanDiagnosticInfo LastDiagnosticInfo { get; }

    bool IsRunning { get; }

    bool IsManuallyDisconnected { get; }

    event Action<LcuCredentials?>? CredentialsChanged;

    event Action<ClientConnectionStatus>? StatusChanged;

    event Action<ProcessScanDiagnosticInfo>? DiagnosticUpdated;

    void Start();

    void Stop();

    Task<ProcessScanDiagnosticInfo> CheckNowAsync(CancellationToken cancellationToken = default);

    void DisconnectManually();

    void ResumeAutoConnect();
}