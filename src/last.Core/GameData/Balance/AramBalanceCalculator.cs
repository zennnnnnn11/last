using System.Globalization;

namespace last.Core.GameData.Balance;

public static class AramBalanceCalculator
{
    private static readonly Definition[] BalanceDefinitions =
    [
        new("damage_dealt", AramBalanceAdjustmentType.DamageDealt, AramBalanceAdjustmentDisplay.Percentage,
            AramBalanceAdjustmentEffectType.Buff, "造成伤害", b => b.DamageDealt),
        new("damage_taken", AramBalanceAdjustmentType.DamageTaken, AramBalanceAdjustmentDisplay.Percentage,
            AramBalanceAdjustmentEffectType.Nerf, "承受伤害", b => b.DamageTaken),
        new("attack_speed", AramBalanceAdjustmentType.AttackSpeed, AramBalanceAdjustmentDisplay.Percentage,
            AramBalanceAdjustmentEffectType.Buff, "攻击速度", b => b.AttackSpeed),
        new("cooldown_reduction", AramBalanceAdjustmentType.AbilityHaste, AramBalanceAdjustmentDisplay.Literal,
            AramBalanceAdjustmentEffectType.Buff, "技能急速", b => b.CooldownReduction),
        new("healing", AramBalanceAdjustmentType.Healing, AramBalanceAdjustmentDisplay.Percentage,
            AramBalanceAdjustmentEffectType.Buff, "治疗效果", b => b.Healing),
        new("tenacity", AramBalanceAdjustmentType.Tenacity, AramBalanceAdjustmentDisplay.Literal,
            AramBalanceAdjustmentEffectType.Buff, "韧性", b => b.Tenacity),
        new("shield_amount", AramBalanceAdjustmentType.Shielding, AramBalanceAdjustmentDisplay.Percentage,
            AramBalanceAdjustmentEffectType.Buff, "护盾效果", b => b.ShieldAmount),
        new("energy_regen", AramBalanceAdjustmentType.EnergyRegen, AramBalanceAdjustmentDisplay.Percentage,
            AramBalanceAdjustmentEffectType.Buff, "能量回复", b => b.EnergyRegen),
        new("area_of_effect_damage", AramBalanceAdjustmentType.AreaOfEffectDamage,
            AramBalanceAdjustmentDisplay.Percentage, AramBalanceAdjustmentEffectType.Buff, "AoE 伤害",
            b => b.AreaOfEffectDamage)
    ];

    public static IReadOnlyList<AramBalanceAdjustment> GetAdjustments(OpggAramBalanceItem balance)
    {
        ArgumentNullException.ThrowIfNull(balance);

        var list = new List<AramBalanceAdjustment>();

        for (var i = 0; i < BalanceDefinitions.Length; i++)
        {
            var def = BalanceDefinitions[i];
            var value = def.ValueGetter(balance);
            var baseline = def.Display == AramBalanceAdjustmentDisplay.Percentage ? 100.0 : 0.0;

            if (Math.Abs(value - baseline) < 0.0001)
                continue;

            var increased = value > baseline;
            var effect = def.EffectType == AramBalanceAdjustmentEffectType.Buff
                ? increased ? AramBalanceAdjustmentEffect.Buffed : AramBalanceAdjustmentEffect.Nerfed
                : increased
                    ? AramBalanceAdjustmentEffect.Nerfed
                    : AramBalanceAdjustmentEffect.Buffed;

            var relativeDelta = def.Display == AramBalanceAdjustmentDisplay.Percentage
                ? value - 100.0
                : value;

            var formattedValue = FormatValue(relativeDelta, def.Display);

            list.Add(new AramBalanceAdjustment(
                def.Field,
                def.Type,
                value,
                def.Display,
                def.EffectType,
                effect,
                i,
                def.DisplayName,
                formattedValue,
                relativeDelta));
        }

        return list;
    }

    public static AramBalanceOverallEffect GetOverallEffect(IReadOnlyList<AramBalanceAdjustment> adjustments)
    {
        ArgumentNullException.ThrowIfNull(adjustments);

        var hasBuff = false;
        var hasNerf = false;

        for (var i = 0; i < adjustments.Count; i++)
            if (adjustments[i].Effect == AramBalanceAdjustmentEffect.Buffed)
                hasBuff = true;
            else if (adjustments[i].Effect == AramBalanceAdjustmentEffect.Nerfed)
                hasNerf = true;

        if (hasBuff && hasNerf)
            return AramBalanceOverallEffect.Mixed;

        if (hasBuff)
            return AramBalanceOverallEffect.Buffed;

        if (hasNerf)
            return AramBalanceOverallEffect.Nerfed;

        return AramBalanceOverallEffect.Neutral;
    }

    public static AramChampionBalance Calculate(OpggAramBalanceItem balance)
    {
        ArgumentNullException.ThrowIfNull(balance);

        var adjustments = GetAdjustments(balance);
        var overall = GetOverallEffect(adjustments);
        return new AramChampionBalance(balance.ChampionId, overall, adjustments);
    }

    public static string FormatValue(double relativeDelta, AramBalanceAdjustmentDisplay display)
    {
        var sign = relativeDelta > 0 ? "+" : "";
        var formatted = relativeDelta.ToString("0.##", CultureInfo.InvariantCulture);

        return display == AramBalanceAdjustmentDisplay.Percentage
            ? $"{sign}{formatted}%"
            : $"{sign}{formatted}";
    }

    private sealed record Definition(
        string Field,
        AramBalanceAdjustmentType Type,
        AramBalanceAdjustmentDisplay Display,
        AramBalanceAdjustmentEffectType EffectType,
        string DisplayName,
        Func<OpggAramBalanceItem, double> ValueGetter);
}