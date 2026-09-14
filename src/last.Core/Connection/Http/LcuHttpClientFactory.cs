using System.Net.Http.Headers;
using System.Net.Security;
using last.Core.Connection.Models;

namespace last.Core.Connection.Http;

public static class LcuHttpClientFactory
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(17500);

    public static SocketsHttpHandler CreateSafeHandler()
    {
        return new SocketsHttpHandler
        {
            UseProxy = false,
            Proxy = null,
            SslOptions =
                new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = static (_, _, _, _) => true
                },
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            EnableMultipleHttp2Connections = true
        };
    }

    public static HttpClient CreateLcuClient(LcuCredentials credentials, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        var handler = CreateSafeHandler();
        var client = new HttpClient(handler, true)
        {
            BaseAddress = new Uri(credentials.BaseUrl),
            Timeout = timeout ?? DefaultTimeout
        };

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials.BasicAuthHeaderValue);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }
}