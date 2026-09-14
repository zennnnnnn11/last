using last.Core.Connection.Models;

namespace last.Core.Connection.Http;

public interface ILcuHandshakeProber
{
    Task<bool> ProbeAsync(LcuCredentials credentials, CancellationToken cancellationToken = default);
}