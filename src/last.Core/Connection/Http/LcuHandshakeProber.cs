using System.Net.Http.Headers;
using last.Core.Connection.Models;

namespace last.Core.Connection.Http;

public sealed class LcuHandshakeProber : ILcuHandshakeProber, IDisposable
{
    public const string PingEndpoint = "riotclient/auth-token";
    private static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromMilliseconds(1500);

    private readonly HttpMessageInvoker _invoker;
    private bool _disposed;

    public LcuHandshakeProber(HttpMessageHandler? handler = null)
    {
        var handlerToUse = handler ?? LcuHttpClientFactory.CreateSafeHandler();
        _invoker = new HttpMessageInvoker(handlerToUse, handler is null);
    }

    public static LcuHandshakeProber Instance { get; } = new();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _invoker.Dispose();
    }

    public async Task<bool> ProbeAsync(LcuCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        if (_disposed)
            return false;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{credentials.BaseUrl.TrimEnd('/')}/{PingEndpoint.TrimStart('/')}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials.BasicAuthHeaderValue);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(DefaultProbeTimeout);

            using var response = await _invoker.SendAsync(request, linkedCts.Token).ConfigureAwait(false);

            return (int)response.StatusCode < 500;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}