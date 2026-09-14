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
///     海克斯/竞技场强化符文高清图标异步加载与多级缓存服务（LRU 容量上限、分槽并发单飞锁、淘汰不 Dispose 安全）。
/// </summary>
public static class AugmentIconLoader
{
    private static readonly LruCache<int, Bitmap> Cache = new(48);

    private static readonly SemaphoreSlim[] Gates = Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    public static int CachedCount => Cache.Count;

    private static SemaphoreSlim GetGate(int id)
    {
        return Gates[(id & 0x7FFFFFFF) % Gates.Length];
    }

    public static async Task<Bitmap?> GetIconAsync(
        int augmentId,
        ILcuConnectionCoordinator coordinator,
        CancellationToken ct = default)
    {
        if (augmentId <= 0)
            return null;

        if (Cache.TryGetValue(augmentId, out var cached))
            return cached;

        var gate = GetGate(augmentId);
        await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (Cache.TryGetValue(augmentId, out cached))
                return cached;

            var bitmap = await DownloadAugmentIconAsync(augmentId, coordinator).ConfigureAwait(false);
            if (bitmap is not null)
                Cache.Set(augmentId, bitmap);

            return bitmap;
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<Bitmap?> DownloadAugmentIconAsync(int augmentId, ILcuConnectionCoordinator coordinator)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var token = cts.Token;

        // 确保强化符文静态字典已下载完成
        if (!coordinator.KiwiAugmentStaticData.IsInitialized)
            try
            {
                await coordinator.KiwiAugmentStaticData.InitializeAsync(token).ConfigureAwait(false);
            }
            catch
            {
                // 忽略初始化异常
            }

        var info = coordinator.KiwiAugmentStaticData.GetAugmentInfo(augmentId);
        if (info == null || string.IsNullOrWhiteSpace(info.IconUrl))
            return null;

        try
        {
            var bytes = await SharedHttpClient.Instance
                .GetByteArrayAsync(info.IconUrl, token)
                .ConfigureAwait(false);

            if (bytes is { Length: > 0 })
            {
                using var ms = new MemoryStream(bytes);
                return Bitmap.DecodeToWidth(ms, 48, BitmapInterpolationMode.MediumQuality);
            }
        }
        catch
        {
            // 忽略下载异常，保留空占位（绝不缓存 null，允许后续重试）
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