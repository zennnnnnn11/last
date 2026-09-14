namespace last.Core.GameData.Static;

/// <summary>
///     召唤师技能名称与 CDN 直链静态数据解析契约。
/// </summary>
public interface ISummonerSpellStaticDataService
{
    /// <summary>
    ///     获取召唤师技能中文名称。未命中时返回兜底名称。
    /// </summary>
    string GetSpellName(int spellId);

    /// <summary>
    ///     获取腾讯官方 CDN 技能图标直链。
    /// </summary>
    string GetSpellIconUri(int spellId);

    /// <summary>
    ///     获取指定技能 ID 的完整元数据模型。
    /// </summary>
    SummonerSpellStaticInfo? GetSpell(int spellId);

    /// <summary>
    ///     获取所有已知召唤师技能列表。
    /// </summary>
    IReadOnlyList<SummonerSpellStaticInfo> GetAllSpells();
}