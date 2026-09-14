namespace last.Core.GameData.Static;

/// <summary>
///     海克斯强化符文规范化静态展示信息。
/// </summary>
public sealed record AugmentStaticInfo(
    int AugmentId,
    string Name,
    string Level,
    string BorderColorHex,
    string BackgroundColorHex,
    string IconUrl,
    string Desc
);