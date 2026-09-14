using System.Text.Json;
using last.Core.Connection.WebSocket;
using last.Core.Services;

namespace last.Core.GameData.Static;

public sealed class GtimgHeroClient : IGtimgHeroClient, IDisposable
{
    public const string DefaultHeroListUrl = "https://game.gtimg.cn/images/lol/act/img/js/heroList/hero_list.js";

    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public GtimgHeroClient(HttpClient? httpClient = null)
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

    public async Task<GtimgHeroList?> GetHeroListAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(DefaultHeroListUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync(
            stream,
            LcuJsonSerializerContext.Default.GtimgHeroList,
            cancellationToken).ConfigureAwait(false);
    }
}