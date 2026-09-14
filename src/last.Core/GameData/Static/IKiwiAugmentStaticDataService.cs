namespace last.Core.GameData.Static;

/// <summary>
///     海克斯强化符文字典静态数据服务契约。
/// </summary>
public interface IKiwiAugmentStaticDataService : IDisposable
{
    /// <summary>
    ///     字典是否已完成初始化加载。
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    ///     异步确保海克斯符文字典已加载完毕。
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     同步获取海克斯符文元数据（未命中时安全返回根据 ID 兜底的模型）。
    /// </summary>
    AugmentStaticInfo GetAugmentInfo(int augmentId);

    /// <summary>
    ///     获取全量已缓存的海克斯符文列表。
    /// </summary>
    IReadOnlyList<AugmentStaticInfo> GetAllAugments();
}