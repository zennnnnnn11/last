using last.Core.MatchHistory.Models;

namespace last.Core.Scoring;

public static class MatchScorer
{
    public static readonly HashSet<int> EnchanterSupportIds =
    [
        40, 16, 117, 350, 37, 267, 902, 432, 44, 497, 888, 43, 26, 427
    ];

    public static readonly HashSet<int> PureTankInitiatorIds =
    [
        14, 54, 111, 89, 201, 526, 98, 516, 31, 223, 32, 154, 113, 33, 78, 57, 12, 53, 412
    ];

    public static readonly HashSet<int> PureOffenseItemIds =
    [
        3089, 3157, 3135, 6655, 4645, 4646, 3115, 3100, 3040, 3041, 3152, 3165, 4628, 4629,
        3003, 3031, 3036, 6676, 3094, 3072, 3142, 6692, 6694, 6698, 3004, 3085, 3046, 6672,
        6673, 6675, 3124, 6697, 6699, 6695, 3139, 3033, 3508
    ];

    public static readonly HashSet<int> PureDefenseItemIds =
    [
        3068, 3075, 3110, 3143, 3065, 3083, 6665, 6667, 2504, 2502, 3001, 4401, 3119, 3193, 3742, 3026
    ];

    public static readonly HashSet<int> BruiserItemIds =
    [
        3071, 3053, 3078, 3052, 6631, 3161, 6610, 6333, 3156, 3074, 3748, 3153, 3181, 4633, 3151, 3116, 3027
    ];

    public static readonly HashSet<int> SupportUtilityItemIds =
    [
        3107, 3504, 6617, 3190, 3109, 6616, 2065, 3222, 3869, 3870, 3877, 3865, 3050, 3871, 3867, 3876
    ];

    public static bool IsArenaMode(int queueId, string? gameMode)
    {
        if (queueId is 1700 or 1710)
            return true;
        if (!string.IsNullOrWhiteSpace(gameMode))
            if (string.Equals(gameMode, "CHERRY", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(gameMode, "ARENA", StringComparison.OrdinalIgnoreCase) ||
                gameMode.Contains("CHERRY", StringComparison.OrdinalIgnoreCase) ||
                gameMode.Contains("ARENA", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    public static (string? mvpPuuid, string? svpPuuid, Dictionary<string, double> scores) EvaluateScores(
        IReadOnlyList<UnifiedParticipant> participants,
        int queueId,
        string? gameMode = null,
        Func<int, IReadOnlyList<string>?>? roleResolver = null,
        Func<int, IReadOnlyList<string>?>? itemCategoriesResolver = null,
        int gameDuration = 0)
    {
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var rawCompressedScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (participants.Count == 0)
            return (null, null, scores);

        if (IsArenaMode(queueId, gameMode))
            return (null, null, scores);

        var isAram = queueId is 450 or 2400 ||
                     string.Equals(gameMode, "ARAM", StringComparison.OrdinalIgnoreCase);

        var blueList = participants.Where(p => p.TeamId == 100).ToList();
        var redList = participants.Where(p => p.TeamId == 200).ToList();
        if (blueList.Count == 0 && redList.Count == 0)
        {
            blueList = participants.Take(participants.Count / 2).ToList();
            redList = participants.Skip(participants.Count / 2).ToList();
        }

        void ScoreTeam(List<UnifiedParticipant> team)
        {
            if (team.Count == 0)
                return;
            var teamSize = Math.Max(1, team.Count);
            var fairDeathShare = 1.0 / teamSize;
            var rawTeamKills = team.Sum(p => p.Kills);
            var rawDeaths = team.Sum(p => p.Deaths);
            var teamDmgArithmetic = Math.Max(1L, team.Sum(p => p.TotalDamageDealtToChampions));
            var teamGeoDmg = Math.Exp(team.Average(p => Math.Log(Math.Max(1L, p.TotalDamageDealtToChampions) + 10.0)));
            var teamDmg = Math.Max(1.0, 0.70 * teamDmgArithmetic + 0.30 * (teamSize * teamGeoDmg));

            var teamTankArithmetic =
                Math.Max(1L, team.Sum(p => p.TotalDamageTaken + (long)(0.40 * p.DamageSelfMitigated)));
            var teamGeoTank = Math.Exp(team.Average(p =>
                Math.Log(Math.Max(1L, p.TotalDamageTaken + (long)(0.40 * p.DamageSelfMitigated)) + 10.0)));
            var teamTank = Math.Max(1.0, 0.70 * teamTankArithmetic + 0.30 * (teamSize * teamGeoTank));

            var teamGold = Math.Max(1, team.Sum(p => p.GoldEarned));
            var rawTeamVision = team.Sum(p => p.VisionScore);
            var teamVision = Math.Max(1, rawTeamVision);

            var (pacingBonusScale, pacingDeathScale) = CalculateContinuousPacingScales(gameDuration, isAram);

            var roles = new ChampionRoleCategory[team.Count];
            var rawBaselines = new RoleBaseline[team.Count];
            for (var i = 0; i < team.Count; i++)
            {
                var p = team[i];
                var role = ClassifyRoleWithItems(
                    p.ChampionId,
                    roleResolver?.Invoke(p.ChampionId),
                    p.Items,
                    itemCategoriesResolver);
                roles[i] = role;
                rawBaselines[i] = GetRoleBaseline(role, isAram);
            }

            var normalizedBaselines = NormalizeTeamBaselines(rawBaselines);
            var kappaKp = isAram ? 4.0 : 3.0;

            for (var i = 0; i < team.Count; i++)
            {
                var p = team[i];
                var role = roles[i];
                var baseline = normalizedBaselines[i];

                var targetKp = isAram ? baseline.AramKp : baseline.SrKp;
                var effectiveKp = (p.Kills + p.Assists + kappaKp * targetKp) / (rawTeamKills + kappaKp);

                var dmgShare = p.TotalDamageDealtToChampions / teamDmg;
                var playerEffectiveTank = p.TotalDamageTaken + (long)(0.40 * p.DamageSelfMitigated);
                var tankShare = playerEffectiveTank / teamTank;
                var deathShare = rawDeaths > 0 ? (double)p.Deaths / rawDeaths : fairDeathShare;
                var goldShare = (double)p.GoldEarned / teamGold;
                var visionShare = rawTeamVision > 0 ? (double)p.VisionScore / teamVision : baseline.VisionShare;
                var kda = (double)(p.Kills + p.Assists) / Math.Max(1, p.Deaths);

                var dpgLogRatio = Math.Log((dmgShare + 1e-4) / (goldShare + 1e-4)) -
                                  Math.Log(baseline.DmgShare / Math.Max(1e-4, baseline.GoldShare));

                var turretBonus = (isAram ? 14.0 : 8.0) *
                                  Math.Tanh(p.DamageDealtToTurrets / (isAram ? 3000.0 : 4500.0));

                var ccDurationRef = Math.Max(10.0, gameDuration / 25.0);
                var ccWeight = role is ChampionRoleCategory.Tank or ChampionRoleCategory.Support ? 6.0
                    : role == ChampionRoleCategory.Fighter ? 4.0 : 2.0;
                var ccBonus = ccWeight * Math.Tanh(p.TimeCCingOthers / ccDurationRef);

                var allyHeal = p.TotalHealsOnTeammates > 0
                    ? p.TotalHealsOnTeammates
                    : p.TotalUnitsHealed > 1 && p.TotalHeal > 0
                        ? p.TotalHeal * ((double)(p.TotalUnitsHealed - 1) / p.TotalUnitsHealed)
                        : 0.0;
                var allyShield = (double)p.TotalDamageShieldedOnTeammates;
                var totalProtection = allyHeal + allyShield;
                if (totalProtection <= 0 && p.EffectiveHealAndShielding > 0 && role == ChampionRoleCategory.Support)
                    totalProtection = p.EffectiveHealAndShielding;

                var protectRef = Math.Max(4000.0, gameDuration * (isAram ? 6.0 : 5.0));
                var protectRoleWeight = role == ChampionRoleCategory.Support ? 1.0
                    : role is ChampionRoleCategory.Tank or ChampionRoleCategory.Fighter ? 0.35 : 0.15;
                var protectBonus = (isAram ? 8.0 : 7.0) * protectRoleWeight * Math.Tanh(totalProtection / protectRef);

                var multiScore = p.PentaKills * 2.5 + p.QuadraKills * 1.2 + Math.Min(1.0, p.TripleKills * 0.4);
                var multikillBonus = Math.Min(isAram ? 4.0 : 3.0, multiScore);

                double rawScore;
                if (isAram)
                {
                    var deadGoldPenalty = 0.0;
                    if (p.GoldSpent > 0)
                    {
                        double unspentGold = Math.Max(0, p.GoldEarned - p.GoldSpent);
                        if (unspentGold > 2500 && unspentGold / Math.Max(1.0, p.GoldEarned) > 0.25)
                            deadGoldPenalty = 6.0 * Math.Tanh((unspentGold - 2500.0) / 2000.0);
                    }

                    var kpSigma = effectiveKp >= baseline.AramKp
                        ? Math.Max(0.01, 1.0 - baseline.AramKp)
                        : Math.Max(0.01, baseline.AramKp);
                    var kpBonus = 22.0 * Math.Tanh((effectiveKp - baseline.AramKp) / kpSigma) * pacingBonusScale;

                    var dmgDev = CalculateSaturatedDeviation(dmgShare, baseline.DmgShare);
                    var tankDev = CalculateSaturatedDeviation(tankShare, baseline.TankShare);
                    if (role == ChampionRoleCategory.Support)
                        tankDev = Math.Max(0.0, tankDev);

                    var collinearFactor = 1.0;
                    if (dmgDev > 0 && tankDev > 0)
                        collinearFactor = 1.0 / (1.0 + 0.50 * (dmgDev * tankDev));

                    var dmgBonus = 32.0 * dmgDev * collinearFactor;
                    var tankBonus = 12.0 * tankDev * collinearFactor;

                    var frontlineCushion = role is ChampionRoleCategory.Tank or ChampionRoleCategory.Fighter
                        ? 0.30 * Math.Max(0.0, tankShare - baseline.TankShare) + (p.TimeCCingOthers > 35 ? 0.05 : 0.0)
                        : 0.0;
                    var netDeathDeviation = deathShare - frontlineCushion - fairDeathShare;
                    var deathPenalty = 10.0 * Math.Tanh(teamSize * netDeathDeviation) * pacingDeathScale;

                    var dpgBonus = 10.0 * Math.Tanh(dpgLogRatio / 0.8);
                    if (role is ChampionRoleCategory.Support or ChampionRoleCategory.Tank)
                        dpgBonus = Math.Max(0.0, dpgBonus);

                    var survivalBonus = CalculateSmoothKdaBonus(kda, 3.0, 6.0, 3.0) * pacingBonusScale;

                    var snowballWeight = role is ChampionRoleCategory.Tank or ChampionRoleCategory.Fighter ? 2.5 : 1.2;
                    var snowballBonus = p.SnowballsHit > 0 ? snowballWeight * Math.Tanh(p.SnowballsHit / 12.0) : 0.0;
                    var packBonus = p.KillsOnRecentlyHealedByAramPack > 0
                        ? Math.Min(1.5, p.KillsOnRecentlyHealedByAramPack * 0.5)
                        : 0.0;

                    rawScore = 60.0 + kpBonus + dmgBonus + tankBonus - deathPenalty + dpgBonus + survivalBonus +
                               turretBonus + ccBonus + protectBonus + multikillBonus + snowballBonus + packBonus -
                               deadGoldPenalty;
                }
                else
                {
                    var kpSigma = effectiveKp >= baseline.SrKp
                        ? Math.Max(0.01, 1.0 - baseline.SrKp)
                        : Math.Max(0.12, baseline.SrKp);
                    var kpBonus = 20.0 * Math.Tanh((effectiveKp - baseline.SrKp) / kpSigma) * pacingBonusScale;

                    if (kpBonus < 0 && p.DamageDealtToTurrets > 2000)
                        kpBonus += 0.5 * turretBonus;

                    var dmgDev = CalculateSaturatedDeviation(dmgShare, baseline.DmgShare);
                    var dmgBonus = 22.0 * dmgDev;

                    var tankDev = CalculateSaturatedDeviation(tankShare, baseline.TankShare);
                    if (role == ChampionRoleCategory.Support)
                        tankDev = Math.Max(0.0, tankDev);
                    var tankBonus = 15.0 * tankDev;

                    var frontlineCushion = role is ChampionRoleCategory.Tank or ChampionRoleCategory.Fighter
                        ? 0.20 * Math.Max(0.0, tankShare - baseline.TankShare) + (p.TimeCCingOthers > 35 ? 0.04 : 0.0)
                        : 0.0;
                    var netDeathDeviation = deathShare - frontlineCushion - fairDeathShare;
                    var deathPenalty = 18.0 * Math.Tanh(teamSize * netDeathDeviation) * pacingDeathScale;

                    var goldDev = CalculateSaturatedDeviation(goldShare, baseline.GoldShare);
                    var goldGap = role == ChampionRoleCategory.Support ? 0.0 : 7.0 * goldDev;

                    var dpgBonus = 7.0 * Math.Tanh(dpgLogRatio / 0.8);
                    if (role is ChampionRoleCategory.Support or ChampionRoleCategory.Tank)
                        dpgBonus = Math.Max(0.0, dpgBonus);
                    var econContribution = goldGap + dpgBonus;

                    var survivalBonus = CalculateSmoothKdaBonus(kda, 2.5, 7.5, 2.5) * pacingBonusScale;

                    var visionDev = CalculateSaturatedDeviation(visionShare, baseline.VisionShare);
                    var visionWeight = role == ChampionRoleCategory.Support ? 18.0
                        : role is ChampionRoleCategory.Tank or ChampionRoleCategory.Fighter ? 4.5 : 3.0;
                    var visionBonus = visionWeight * visionDev;
                    if (role != ChampionRoleCategory.Support && visionBonus < 0.0)
                        visionBonus *= 0.5;

                    var objectivePoints = p.DragonTakedowns * 1.5 + p.BaronTakedowns * 2.0 +
                                          p.RiftHeraldTakedowns * 1.0 + p.VoidMonsterKill * 0.5;
                    var objectiveWeight = role is ChampionRoleCategory.Tank or ChampionRoleCategory.Fighter ? 4.5 : 3.0;
                    var objectiveBonus = objectiveWeight * Math.Tanh(objectivePoints / 6.0);
                    var stealBonus = Math.Min(4.0, p.EpicMonsterSteals * 2.5);
                    var neutralObjContribution = objectiveBonus + stealBonus;

                    var soloKillBonus = 3.0 * Math.Tanh(p.SoloKills / 2.0);
                    var plateBonus = 2.0 * Math.Tanh(p.TurretPlatesTaken / 3.0);
                    var laningBonus = soloKillBonus + plateBonus;

                    var csPerMinute = gameDuration > 60 ? p.Cs / (gameDuration / 60.0) : 0.0;
                    var csBonus = 0.0;
                    if (role == ChampionRoleCategory.Carry && csPerMinute > 0.0)
                        csBonus = 3.0 * Math.Tanh((csPerMinute - 7.5) / 2.0);

                    rawScore = 60.0 + kpBonus + dmgBonus + tankBonus - deathPenalty + econContribution + survivalBonus +
                               turretBonus + ccBonus + protectBonus + visionBonus + neutralObjContribution +
                               laningBonus + csBonus + multikillBonus;
                }

                var rawCompressed = SoftClampScore(rawScore);
                rawCompressedScores[p.Puuid] = rawCompressed;
                var finalScore = Math.Round(rawCompressed, 1);
                scores[p.Puuid] = finalScore;
            }
        }

        ScoreTeam(blueList);
        ScoreTeam(redList);

        var winners = participants.Where(p => p.IsWin).ToList();
        var losers = participants.Where(p => !p.IsWin).ToList();

        var mvp = winners.Count > 0
            ? winners.OrderByDescending(p => rawCompressedScores.GetValueOrDefault(p.Puuid, 0.0)).First().Puuid
            : null;
        var svp = losers.Count > 0
            ? losers.OrderByDescending(p => rawCompressedScores.GetValueOrDefault(p.Puuid, 0.0)).First().Puuid
            : null;

        return (mvp, svp, scores);
    }

    public static ItemArchetype ClassifyItem(int itemId, IReadOnlyList<string>? categories)
    {
        if (itemId <= 0)
            return ItemArchetype.None;
        if (PureOffenseItemIds.Contains(itemId))
            return ItemArchetype.Offense;
        if (PureDefenseItemIds.Contains(itemId))
            return ItemArchetype.Defense;
        if (BruiserItemIds.Contains(itemId))
            return ItemArchetype.Bruiser;
        if (SupportUtilityItemIds.Contains(itemId))
            return ItemArchetype.Support;

        if (categories != null && categories.Count > 0)
        {
            var hasSupport = categories.Any(c => string.Equals(c, "GoldPer", StringComparison.OrdinalIgnoreCase) ||
                                                 string.Equals(c, "Aura", StringComparison.OrdinalIgnoreCase));
            var hasDamage = categories.Any(c => string.Equals(c, "Damage", StringComparison.OrdinalIgnoreCase) ||
                                                string.Equals(c, "SpellDamage", StringComparison.OrdinalIgnoreCase) ||
                                                string.Equals(c, "CriticalStrike",
                                                    StringComparison.OrdinalIgnoreCase) ||
                                                string.Equals(c, "ArmorPenetration",
                                                    StringComparison.OrdinalIgnoreCase) ||
                                                string.Equals(c, "MagicPenetration",
                                                    StringComparison.OrdinalIgnoreCase));
            var hasDefense = categories.Any(c => string.Equals(c, "Armor", StringComparison.OrdinalIgnoreCase) ||
                                                 string.Equals(c, "SpellBlock", StringComparison.OrdinalIgnoreCase) ||
                                                 string.Equals(c, "Health", StringComparison.OrdinalIgnoreCase));

            if (hasSupport)
                return ItemArchetype.Support;
            if (hasDamage && hasDefense)
                return ItemArchetype.Bruiser;
            if (hasDamage)
                return ItemArchetype.Offense;
            if (hasDefense)
                return ItemArchetype.Defense;
        }

        return ItemArchetype.None;
    }

    public static ChampionRoleCategory ClassifyRoleWithItems(
        int championId,
        IReadOnlyList<string>? championRoles,
        IReadOnlyList<int>? items,
        Func<int, IReadOnlyList<string>?>? itemCategoriesResolver)
    {
        var baseRole = ClassifyRole(championId, championRoles);
        if (items == null || items.Count == 0)
            return baseRole;

        var offenseCount = 0;
        var defenseCount = 0;
        var bruiserCount = 0;
        var supportCount = 0;

        foreach (var itemId in items)
        {
            if (itemId <= 0)
                continue;
            var archetype = ClassifyItem(itemId, itemCategoriesResolver?.Invoke(itemId));
            switch (archetype)
            {
                case ItemArchetype.Offense:
                    offenseCount++;
                    break;
                case ItemArchetype.Defense:
                    defenseCount++;
                    break;
                case ItemArchetype.Bruiser:
                    bruiserCount++;
                    break;
                case ItemArchetype.Support:
                    supportCount++;
                    break;
            }
        }

        if (offenseCount >= 2 && defenseCount == 0 && supportCount == 0 && bruiserCount <= 1)
            return ChampionRoleCategory.Carry;

        if (defenseCount >= 2 && offenseCount == 0 && bruiserCount <= 1)
            return ChampionRoleCategory.Tank;

        if (supportCount >= 2 && offenseCount <= 1 && defenseCount <= 1)
            return ChampionRoleCategory.Support;

        if (supportCount >= 2 && offenseCount >= 2)
            return ChampionRoleCategory.General;

        if (baseRole == ChampionRoleCategory.Carry && (bruiserCount >= 2 || (bruiserCount >= 1 && defenseCount >= 1)))
            return ChampionRoleCategory.Fighter;

        return baseRole;
    }

    public static ChampionRoleCategory ClassifyRole(int championId, IReadOnlyList<string>? roles)
    {
        if (EnchanterSupportIds.Contains(championId))
            return ChampionRoleCategory.Support;
        if (PureTankInitiatorIds.Contains(championId))
            return ChampionRoleCategory.Tank;

        if (roles != null && roles.Count > 0)
        {
            if (roles.Any(r => string.Equals(r, "support", StringComparison.OrdinalIgnoreCase)) &&
                !roles.Any(r => string.Equals(r, "marksman", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(r, "assassin", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(r, "fighter", StringComparison.OrdinalIgnoreCase)))
                return ChampionRoleCategory.Support;

            if (roles.Any(r => string.Equals(r, "tank", StringComparison.OrdinalIgnoreCase)) &&
                !roles.Any(r => string.Equals(r, "marksman", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(r, "assassin", StringComparison.OrdinalIgnoreCase)))
                return ChampionRoleCategory.Tank;

            if (roles.Any(r => string.Equals(r, "fighter", StringComparison.OrdinalIgnoreCase)))
                return ChampionRoleCategory.Fighter;

            if (roles.Any(r => string.Equals(r, "marksman", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(r, "mage", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(r, "assassin", StringComparison.OrdinalIgnoreCase)))
                return ChampionRoleCategory.Carry;
        }

        return ChampionRoleCategory.General;
    }

    public static IReadOnlyList<RoleBaseline> NormalizeTeamBaselines(IReadOnlyList<RoleBaseline> rawBaselines)
    {
        if (rawBaselines.Count == 0)
            return [];

        var sumDmg = 0.0;
        var sumTank = 0.0;
        var sumGold = 0.0;
        var sumVision = 0.0;

        for (var i = 0; i < rawBaselines.Count; i++)
        {
            sumDmg += rawBaselines[i].DmgShare;
            sumTank += rawBaselines[i].TankShare;
            sumGold += rawBaselines[i].GoldShare;
            sumVision += rawBaselines[i].VisionShare;
        }

        var invDmg = sumDmg > 1e-6 ? 1.0 / sumDmg : 0.0;
        var invTank = sumTank > 1e-6 ? 1.0 / sumTank : 0.0;
        var invGold = sumGold > 1e-6 ? 1.0 / sumGold : 0.0;
        var invVision = sumVision > 1e-6 ? 1.0 / sumVision : 0.0;
        var uniformShare = 1.0 / rawBaselines.Count;

        var normalized = new RoleBaseline[rawBaselines.Count];
        for (var i = 0; i < rawBaselines.Count; i++)
        {
            var raw = rawBaselines[i];
            normalized[i] = new RoleBaseline(
                sumDmg > 1e-6 ? raw.DmgShare * invDmg : uniformShare,
                sumTank > 1e-6 ? raw.TankShare * invTank : uniformShare,
                sumGold > 1e-6 ? raw.GoldShare * invGold : uniformShare,
                raw.AramKp,
                raw.SrKp,
                sumVision > 1e-6 ? raw.VisionShare * invVision : uniformShare);
        }

        return normalized;
    }

    public static RoleBaseline GetRoleBaseline(ChampionRoleCategory role, bool isAram = true)
    {
        if (isAram)
            return role switch
            {
                ChampionRoleCategory.Support => new RoleBaseline(0.09, 0.13, 0.18, 0.80, 0.50, 0.05),
                ChampionRoleCategory.Tank => new RoleBaseline(0.16, 0.32, 0.19, 0.67, 0.40, 0.05),
                ChampionRoleCategory.Fighter => new RoleBaseline(0.20, 0.22, 0.20, 0.66, 0.42, 0.05),
                ChampionRoleCategory.Carry => new RoleBaseline(0.22, 0.16, 0.21, 0.66, 0.48, 0.05),
                _ => new RoleBaseline(0.20, 0.20, 0.20, 0.66, 0.45, 0.05)
            };

        return role switch
        {
            ChampionRoleCategory.Support => new RoleBaseline(0.09, 0.14, 0.15, 0.70, 0.50, 0.38),
            ChampionRoleCategory.Tank => new RoleBaseline(0.18, 0.26, 0.18, 0.65, 0.38, 0.18),
            ChampionRoleCategory.Fighter => new RoleBaseline(0.20, 0.22, 0.20, 0.62, 0.38, 0.16),
            ChampionRoleCategory.Carry => new RoleBaseline(0.23, 0.18, 0.22, 0.66, 0.47, 0.14),
            _ => new RoleBaseline(0.20, 0.20, 0.20, 0.65, 0.40, 0.16)
        };
    }

    public static double CalculateLogRatioDeviation(double share, double baselineShare, double saturationLimit = 2.0,
        double scale = 1.0)
    {
        if (baselineShare <= 0.0)
            return 0.0;
        var eps = 0.01 * baselineShare;
        var ratio = (share + eps) / (baselineShare + eps);
        var logRatio = Math.Log(ratio);
        return saturationLimit * Math.Tanh(logRatio / (saturationLimit * scale));
    }

    public static double CalculateSaturatedDeviation(double share, double baselineShare, double saturationLimit = 2.0)
    {
        return CalculateLogRatioDeviation(share, baselineShare, saturationLimit);
    }

    public static (double bonusScale, double deathScale) CalculateContinuousPacingScales(int durationSeconds,
        bool isAram)
    {
        if (durationSeconds <= 0)
            return (1.0, 1.0);

        if (isAram)
        {
            var earlySigmoid = 1.0 / (1.0 + Math.Exp(-(durationSeconds - 600.0) / 75.0));
            var bonusScale = 0.88 + 0.12 * earlySigmoid;

            var lateSigmoid = 1.0 / (1.0 + Math.Exp(-(durationSeconds - 1400.0) / 90.0));
            var deathScale = 1.00 + 0.15 * lateSigmoid;

            return (bonusScale, deathScale);
        }
        else
        {
            var earlySigmoid = 1.0 / (1.0 + Math.Exp(-(durationSeconds - 1000.0) / 100.0));
            var bonusScale = 0.88 + 0.12 * earlySigmoid;

            var lateSigmoid = 1.0 / (1.0 + Math.Exp(-(durationSeconds - 2150.0) / 120.0));
            var deathScale = 1.00 + 0.15 * lateSigmoid;

            return (bonusScale, deathScale);
        }
    }

    public static double CalculateSmoothKdaBonus(double kda, double threshold, double maxBonus, double divisor)
    {
        if (kda <= threshold)
            return 0.0;
        var delta = kda - threshold;
        var softFactor = delta / (delta + 1.0);
        return maxBonus * Math.Tanh(delta / divisor) * softFactor;
    }

    public static double SoftClampScore(double rawScore)
    {
        if (rawScore >= 60.0)
        {
            var delta = rawScore - 60.0;
            return 60.0 + 40.0 * Math.Tanh(delta / 35.0);
        }
        else
        {
            var delta = rawScore - 60.0;
            return 60.0 + 50.0 * Math.Tanh(delta / 43.75);
        }
    }
}