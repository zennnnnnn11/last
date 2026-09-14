namespace last.Core.GameData.Static;

/// <summary>
///     腾讯 Gtimg 海克斯强化符文原始数据客户端契约。
/// </summary>
public interface IGtimgKiwiClient
{
    /// <summary>
    ///     从 Gtimg 静态 CDN 获取海克斯强化符文全量列表。
    /// </summary>
    Task<IReadOnlyList<GtimgKiwiAugment>?> GetAugmentsAsync(CancellationToken cancellationToken = default);
}