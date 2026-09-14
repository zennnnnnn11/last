namespace last.Core.GameData.Static;

/// <summary>
///     召唤师技能静态元数据信息模型。
/// </summary>
public sealed record SummonerSpellStaticInfo(
    int SpellId,
    string Name,
    string AssetCode,
    string IconUri
);