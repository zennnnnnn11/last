using System.Collections.Concurrent;

namespace last.Core.GameData.Static;

/// <summary>
///     海克斯大乱斗与竞技场强化符文加载与元数据缓存服务实现。
/// </summary>
public sealed class KiwiAugmentStaticDataService : IKiwiAugmentStaticDataService
{
    private readonly ConcurrentDictionary<int, AugmentStaticInfo> _augmentDict = new();
    private readonly IGtimgKiwiClient _client;
    private readonly Lock _initLock = new();
    private Task? _initTask;
    private volatile bool _isInitialized;

    public KiwiAugmentStaticDataService(IGtimgKiwiClient? client = null)
    {
        _client = client ?? new GtimgKiwiClient();
        _ = InitializeAsync();
    }

    /// <inheritdoc />
    public bool IsInitialized => _isInitialized;

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return Task.CompletedTask;

        lock (_initLock)
        {
            if (_isInitialized) return Task.CompletedTask;
            if (_initTask != null && _initTask.IsCompleted && !_isInitialized)
                _initTask = null;

            _initTask ??= FetchAugmentDictionaryAsync(cancellationToken);
            return _initTask;
        }
    }

    /// <inheritdoc />
    public AugmentStaticInfo GetAugmentInfo(int augmentId)
    {
        if (_augmentDict.TryGetValue(augmentId, out var info))
            return info;

        return new AugmentStaticInfo(
            augmentId,
            $"强化 {augmentId}",
            "kSilver",
            "#94A3B8",
            "#1E293B",
            string.Empty,
            string.Empty
        );
    }

    /// <inheritdoc />
    public IReadOnlyList<AugmentStaticInfo> GetAllAugments()
    {
        return _augmentDict.Values.ToList();
    }

    public void Dispose()
    {
        _augmentDict.Clear();
        if (_client is IDisposable d) d.Dispose();
    }

    public static AugmentStaticInfo MapToStaticInfo(GtimgKiwiAugment item)
    {
        var borderColor = ResolveBorderColor(item.Level);
        var bgColor = ResolveBackgroundColor(item.Level);
        return new AugmentStaticInfo(
            item.AugmentId,
            string.IsNullOrWhiteSpace(item.NameCn) ? $"强化 {item.AugmentId}" : item.NameCn,
            item.Level ?? "kSilver",
            borderColor,
            bgColor,
            item.SmallIcon ?? string.Empty,
            item.Desc ?? string.Empty
        );
    }

    public static string ResolveBorderColor(string? level)
    {
        return level switch
        {
            "kPrismatic" => "#A855F7", // 紫晶/棱彩：发光紫罗兰光环
            "kGold" => "#F59E0B", // 曜金：明亮耀金光环
            "kBronze" => "#CD7F32", // 青铜：古铜光环
            _ => "#94A3B8" // 秘银：冷钢银光环
        };
    }

    public static string ResolveBackgroundColor(string? level)
    {
        return level switch
        {
            "kPrismatic" => "#3B1B54", // 棱彩：深紫罗兰宝石底色
            "kGold" => "#382006", // 曜金：深琥珀金底色
            "kBronze" => "#27170E", // 青铜：深古铜底色
            _ => "#1E293B" // 秘银：深板岩钢底色
        };
    }

    private async Task FetchAugmentDictionaryAsync(CancellationToken cancellationToken)
    {
        try
        {
            var list = await _client.GetAugmentsAsync(cancellationToken).ConfigureAwait(false);
            if (list != null)
            {
                foreach (var item in list)
                    _augmentDict[item.AugmentId] = MapToStaticInfo(item);
                _isInitialized = true;
            }
        }
        catch
        {
            // 失败时安全静默，后续通过 GetAugmentInfo 兜底降级
        }
        finally
        {
            if (!_isInitialized)
                lock (_initLock)
                {
                    _initTask = null;
                }
        }
    }
}