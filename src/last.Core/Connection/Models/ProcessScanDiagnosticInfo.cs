namespace last.Core.Connection.Models;

public sealed record ProcessScanDiagnosticInfo(
    ClientConnectionStatus Status,
    IReadOnlyList<int> DetectedPids,
    LcuCredentials? Credentials,
    DateTimeOffset LastScanTime,
    int FailureCountWithoutCommandLine = 0,
    string? ErrorMessage = null);