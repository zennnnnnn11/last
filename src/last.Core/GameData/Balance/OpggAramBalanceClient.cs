using System.Text.Json;
using last.Core.Connection.WebSocket;
using last.Core.Services;

namespace last.Core.GameData.Balance;

public sealed class OpggAramBalanceClient : IOpggAramBalanceClient, IDisposable
{
    public const string DefaultAramBalanceUrl = "https://lol-api-champion.op.gg/api/contents/aram-balance";

    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public OpggAramBalanceClient(HttpClient? httpClient = null)
    {
        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _ownsClient = false;
        }
        else
        {
            _httpClient = SharedHttpClient.Instance;
            _ownsClient = false;
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
            _httpClient.Dispose();
    }

    public async Task<IReadOnlyList<OpggAramBalanceItem>?> GetAramBalanceAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(DefaultAramBalanceUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync(
            stream,
            LcuJsonSerializerContext.Default.OpggAramBalanceResponse,
            cancellationToken).ConfigureAwait(false);

        return result?.Data;
    }
}