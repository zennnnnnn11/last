using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using last.Core.Connection.WebSocket;
using last.Core.MatchHistory.Models;

namespace last.Core.MatchHistory.Api;

public sealed class SgpMatchHistoryClient : ISgpMatchHistoryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = LcuJsonSerializerContext.Default
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    public SgpMatchHistoryClient(HttpClient? httpClient = null)
    {
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                EnableMultipleHttp2Connections = true,
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = static (sender, certificate, _, errors) =>
                    {
                        if (errors == SslPolicyErrors.None)
                            return true;

                        // 1. 优先校验 SslStream 的目标主机名 (SNI 握手目标)
                        if (sender is SslStream { TargetHostName: { } host } &&
                            (host.EndsWith(".lol.qq.com", StringComparison.OrdinalIgnoreCase) ||
                             host.Equals("lol.qq.com", StringComparison.OrdinalIgnoreCase)))
                            return true;

                        // 2. 备用校验：核对服务器证书 Subject 是否属于腾讯官方域名
                        if (certificate is not null)
                        {
                            var subject = certificate.Subject;
                            if (subject.Contains("lol.qq.com", StringComparison.OrdinalIgnoreCase) ||
                                subject.Contains(".qq.com", StringComparison.OrdinalIgnoreCase))
                                return true;
                        }

                        return false;
                    }
                }
            };
            _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };
            _ownsHttpClient = true;
        }
    }

    public Task<SgpMatchHistoryLol?> GetMatchHistorySummaryAsync(
        string puuid,
        string accessToken,
        string? platformId = null,
        int startIndex = 0,
        int count = 20,
        IReadOnlyList<string>? tags = null,
        string? tagsQueryType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(puuid);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var gatewayUrl = TencentSgpServerConfig.GetGatewayUrl(platformId);
        var sb = new StringBuilder(128);
        sb.Append(gatewayUrl);
        sb.Append("/match-history-query/v1/products/lol/player/");
        sb.Append(puuid);
        sb.Append("/SUMMARY?startIndex=");
        sb.Append(startIndex);
        sb.Append("&count=");
        sb.Append(count);

        if (tags != null && tags.Count > 0)
        {
            foreach (var t in tags)
                if (!string.IsNullOrWhiteSpace(t))
                {
                    sb.Append("&tag=");
                    sb.Append(Uri.EscapeDataString(t));
                }

            var queryType = !string.IsNullOrWhiteSpace(tagsQueryType) ? tagsQueryType : tags.Count > 1 ? "OR" : null;
            if (!string.IsNullOrWhiteSpace(queryType))
            {
                sb.Append("&tagsQueryType=");
                sb.Append(Uri.EscapeDataString(queryType));
            }
        }

        return SendAsync<SgpMatchHistoryLol>(sb.ToString(), accessToken, cancellationToken);
    }

    public Task<SgpGameSummaryLol?> GetGameSummaryAsync(
        long gameId,
        string accessToken,
        string? platformId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var gatewayUrl = TencentSgpServerConfig.GetGatewayUrl(platformId);
        var subId = TencentSgpServerConfig.NormalizePlatformId(platformId);
        var url = $"{gatewayUrl}/match-history-query/v1/products/lol/{subId}_{gameId}/SUMMARY";

        return SendAsync<SgpGameSummaryLol>(url, accessToken, cancellationToken);
    }

    public Task<SgpGameDetailsLol?> GetGameDetailsAsync(
        long gameId,
        string accessToken,
        string? platformId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var gatewayUrl = TencentSgpServerConfig.GetGatewayUrl(platformId);
        var subId = TencentSgpServerConfig.NormalizePlatformId(platformId);
        var url = $"{gatewayUrl}/match-history-query/v1/products/lol/{subId}_{gameId}/DETAILS";

        return SendAsync<SgpGameDetailsLol>(url, accessToken, cancellationToken);
    }

    public Task<SgpGsmLedgeRegion?> GetGsmByPuuidAsync(
        string puuid,
        string sessionToken,
        string? platformId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(puuid);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

        var gatewayUrl = TencentSgpServerConfig.GetGatewayUrl(platformId);
        var subId = TencentSgpServerConfig.NormalizePlatformId(platformId);
        var url = $"{gatewayUrl}/gsm/v1/ledge/region/{subId}/puuid/{puuid}";

        return SendAsync<SgpGsmLedgeRegion>(url, sessionToken, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_ownsHttpClient)
            _httpClient.Dispose();
    }

    public Task<SgpMatchHistoryLol?> GetMatchHistorySummaryAsync(
        string puuid,
        string accessToken,
        string? platformId = null,
        int startIndex = 0,
        int count = 20,
        string? tag = null,
        string? tagsQueryType = null,
        CancellationToken cancellationToken = default)
    {
        var tags = !string.IsNullOrWhiteSpace(tag) ? new[] { tag } : null;
        return GetMatchHistorySummaryAsync(puuid, accessToken, platformId, startIndex, count, tags, tagsQueryType,
            cancellationToken);
    }

    private async Task<T?> SendAsync<T>(string url, string bearerToken, CancellationToken cancellationToken)
        where T : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new SgpAuthenticationException(
                $"SGP request to '{url}' failed with status {(int)response.StatusCode} {response.ReasonPhrase}",
                response.StatusCode);

        if (!response.IsSuccessStatusCode)
            return null;

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            if (JsonOptions.TryGetTypeInfo(typeof(T), out var rawInfo) && rawInfo is JsonTypeInfo<T> typeInfo)
                return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false);

            throw new InvalidOperationException(
                $"Type '{typeof(T).FullName}' must be registered in LcuJsonSerializerContext for Native AOT support.");
        }
    }
}

public sealed class SgpAuthenticationException : HttpRequestException
{
    public SgpAuthenticationException(string message, HttpStatusCode statusCode)
        : base(message, null, statusCode)
    {
    }
}