using System.Text.Json.Serialization;

namespace last.Core.GameData.Balance;

public enum AramBalanceAdjustmentType
{
    DamageDealt,
    DamageTaken,
    AttackSpeed,
    AbilityHaste,
    Healing,
    Tenacity,
    Shielding,
    EnergyRegen,
    AreaOfEffectDamage
}

public enum AramBalanceAdjustmentDisplay
{
    Percentage,
    Literal
}

public enum AramBalanceAdjustmentEffectType
{
    Buff,
    Nerf
}

public enum AramBalanceAdjustmentEffect
{
    Buffed,
    Nerfed
}

public enum AramBalanceOverallEffect
{
    Neutral,
    Buffed,
    Nerfed,
    Mixed
}

public sealed record AramBalanceAdjustment(
    string Field,
    AramBalanceAdjustmentType Type,
    double Value,
    AramBalanceAdjustmentDisplay Display,
    AramBalanceAdjustmentEffectType EffectType,
    AramBalanceAdjustmentEffect Effect,
    int Order,
    string DisplayName,
    string FormattedValue,
    double RelativeDelta);

public sealed record AramChampionBalance(
    int ChampionId,
    AramBalanceOverallEffect OverallEffect,
    IReadOnlyList<AramBalanceAdjustment> Adjustments)
{
    public static readonly AramChampionBalance Neutral = new(0, AramBalanceOverallEffect.Neutral, []);
}

public sealed record OpggAramBalanceItem(
    [property: JsonPropertyName("champion_id")]
    int ChampionId,
    [property: JsonPropertyName("attack_speed")]
    double AttackSpeed = 100,
    [property: JsonPropertyName("damage_dealt")]
    double DamageDealt = 100,
    [property: JsonPropertyName("damage_taken")]
    double DamageTaken = 100,
    [property: JsonPropertyName("cooldown_reduction")]
    double CooldownReduction = 0,
    [property: JsonPropertyName("healing")]
    double Healing = 100,
    [property: JsonPropertyName("tenacity")]
    double Tenacity = 0,
    [property: JsonPropertyName("shield_amount")]
    double ShieldAmount = 100,
    [property: JsonPropertyName("energy_regen")]
    double EnergyRegen = 100,
    [property: JsonPropertyName("area_of_effect_damage")]
    double AreaOfEffectDamage = 100,
    [property: JsonPropertyName("default")]
    bool Default = false);

public sealed record OpggAramBalanceResponse(
    [property: JsonPropertyName("data")] IReadOnlyList<OpggAramBalanceItem>? Data = null);