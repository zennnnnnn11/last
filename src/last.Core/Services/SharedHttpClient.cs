using System.Net;

namespace last.Core.Services;

public static class SharedHttpClient
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private static readonly SocketsHttpHandler DefaultSocketsHandler = new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        ConnectTimeout = TimeSpan.FromSeconds(5),
        MaxConnectionsPerServer = 16,
        EnableMultipleHttp2Connections = true,
        AutomaticDecompression = DecompressionMethods.All
    };

    public static readonly HttpClient Instance = CreateSharedClient();

    private static HttpClient CreateSharedClient()
    {
        var client = new HttpClient(DefaultSocketsHandler, false) { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        return client;
    }
}