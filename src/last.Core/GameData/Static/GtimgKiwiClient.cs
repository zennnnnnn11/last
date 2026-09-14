using System.Text.Json;
using last.Core.Connection.WebSocket;

namespace last.Core.GameData.Static;

/// <summary>
///     腾讯 Gtimg 海克斯强化符文静态数据客户端实现。
/// </summary>
public sealed class GtimgKiwiClient : IGtimgKiwiClient, IDisposable
{
    public const string DefaultAugmentsUrl = "https://game.gtimg.cn/images/lol/act/img/js/kiwi/kiwi_augments.json";

    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public GtimgKiwiClient(HttpClient? httpClient = null)
    {
        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _ownsClient = false;
        }
        else
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            _ownsClient = true;
        }
    }

    public void Dispose()
    {
        if (_ownsClient) _httpClient.Dispose();
    }

    public async Task<IReadOnlyList<GtimgKiwiAugment>?> GetAugmentsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient
            .GetAsync(DefaultAugmentsUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var list = await JsonSerializer.DeserializeAsync(
            stream,
            LcuJsonSerializerContext.Default.ListGtimgKiwiAugment,
            cancellationToken).ConfigureAwait(false);

        return list;
    }
}