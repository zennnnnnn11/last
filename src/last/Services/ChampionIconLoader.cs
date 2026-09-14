using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using last.Core.Common;
using last.Core.Connection.Services;
using last.Core.Services;

namespace last.Services;

/// <summary>
///     英雄高清头像异步加载与多级缓存服务（LRU 容量上限、分槽并发单飞锁、淘汰不 Dispose 安全）。
/// </summary>
public static class ChampionIconLoader
{
    private static readonly LruCache<int, Bitmap> Cache = new(80);

    private static readonly SemaphoreSlim[] Gates = Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    public static int CachedCount => Cache.Count;

    private static SemaphoreSlim GetGate(int id)
    {
        return Gates[(id & 0x7FFFFFFF) % Gates.Length];
    }

    public static async Task<Bitmap?> GetIconAsync(
        int championId,
        ILcuConnectionCoordinator coordinator,
        CancellationToken ct = default)
    {
        if (championId <= 0)
            return null;

        // 1. 内存一级 LRU 缓存快速命中
        if (Cache.TryGetValue(championId, out var cached))
            return cached;

        // 2. 并发分槽单飞锁（32 个常驻分槽，杜绝锁对象无限泄漏）
        var gate = GetGate(championId);
        await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (Cache.TryGetValue(championId, out cached))
                return cached;

            // 3. 全局公共资源下载（使用内部独立超时，不被调用方局部 Token 取消所毒化）
            var bitmap = await DownloadChampionIconAsync(championId, coordinator).ConfigureAwait(false);
            if (bitmap is not null)
                Cache.Set(championId, bitmap);

            return bitmap;
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<Bitmap?> DownloadChampionIconAsync(int championId, ILcuConnectionCoordinator coordinator)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = cts.Token;

        try
        {
            // 1. 优先从客户端本地 REST 接口读取
            var bytes = await coordinator.StateCoordinator.RestClient
                .GetByteArrayAsync($"/lol-game-data/assets/v1/champion-icons/{championId}.png", token)
                .ConfigureAwait(false);

            if (bytes is { Length: > 0 })
            {
                using var ms = new MemoryStream(bytes);
                return Bitmap.DecodeToWidth(ms, 80, BitmapInterpolationMode.MediumQuality);
            }
        }
        catch
        {
            // 忽略本地接口读取失败，降级到 CDN
        }

        try
        {
            // 2. 降级：通过官方静态服务或者 CDN 读取
            var uri = coordinator.ChampionStaticData.GetIconUri(championId);
            if (uri.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var cdnBytes = await SharedHttpClient.Instance
                    .GetByteArrayAsync(uri, token)
                    .ConfigureAwait(false);

                if (cdnBytes is { Length: > 0 })
                {
                    using var ms = new MemoryStream(cdnBytes);
                    return Bitmap.DecodeToWidth(ms, 80, BitmapInterpolationMode.MediumQuality);
                }
            }
        }
        catch
        {
            // CDN 降级失败，保留空占位（绝不缓存 null，允许后续重试）
        }

        return null;
    }

    /// <summary>
    ///     重置内存缓存。仅解绑字典引用，交由 GC 自然回收，绝不主动调用 Dispose 避免界面正在使用的图像失效。
    /// </summary>
    public static void Clear()
    {
        Cache.Clear();
    }
}