namespace last.Core.GameData.Static;

/// <summary>
///     召唤师技能名称与 CDN 直链静态数据解析服务实现。
/// </summary>
public sealed class SummonerSpellStaticDataService : ISummonerSpellStaticDataService
{
    public const string GtimgSpellBaseUrl = "https://game.gtimg.cn/images/lol/act/img/spell";

    private static readonly Dictionary<int, (string Name, string AssetCode)> KnownSpells = new()
    {
        [1] = ("净化", "SummonerBoost"),
        [3] = ("虚弱", "SummonerExhaust"),
        [4] = ("闪现", "SummonerFlash"),
        [6] = ("幽灵疾步", "SummonerHaste"),
        [7] = ("治疗术", "SummonerHeal"),
        [11] = ("惩戒", "SummonerSmite"),
        [12] = ("传送", "SummonerTeleport"),
        [13] = ("清晰术", "SummonerMana"),
        [14] = ("引燃", "SummonerDot"),
        [21] = ("屏障", "SummonerBarrier"),
        [32] = ("极速标记", "SummonerSnowball"),
        [39] = ("超极速标记", "SummonerSnowURFSnowball_Mark"),
        [2201] = ("逃脱", "SummonerCherryHold"),
        [2202] = ("闪现", "SummonerCherryFlash")
    };

    private static readonly List<SummonerSpellStaticInfo> AllCachedSpells;

    static SummonerSpellStaticDataService()
    {
        var list = new List<SummonerSpellStaticInfo>(KnownSpells.Count);
        foreach (var (id, (name, code)) in KnownSpells)
            list.Add(new SummonerSpellStaticInfo(id, name, code, $"{GtimgSpellBaseUrl}/{code}.png"));
        AllCachedSpells = list;
    }

    /// <inheritdoc />
    public string GetSpellName(int spellId)
    {
        if (spellId <= 0) return "无技能";
        return KnownSpells.TryGetValue(spellId, out var tuple) ? tuple.Name : $"技能 {spellId}";
    }

    /// <inheritdoc />
    public string GetSpellIconUri(int spellId)
    {
        if (spellId <= 0) return string.Empty;
        return KnownSpells.TryGetValue(spellId, out var tuple)
            ? $"{GtimgSpellBaseUrl}/{tuple.AssetCode}.png"
            : string.Empty;
    }

    /// <inheritdoc />
    public SummonerSpellStaticInfo? GetSpell(int spellId)
    {
        if (KnownSpells.TryGetValue(spellId, out var tuple))
            return new SummonerSpellStaticInfo(spellId, tuple.Name, tuple.AssetCode,
                $"{GtimgSpellBaseUrl}/{tuple.AssetCode}.png");
        return null;
    }

    /// <inheritdoc />
    public IReadOnlyList<SummonerSpellStaticInfo> GetAllSpells()
    {
        return AllCachedSpells;
    }
}