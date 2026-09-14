using last.Core.MatchHistory.Models;

namespace last.Core.MatchHistory.Adapters;

public static class MatchDataAdapter
{
    public static UnifiedMatchSummary LcuToUnified(LcuGameSummary game, string? targetPuuid)
    {
        ArgumentNullException.ThrowIfNull(game);

        var identities = game.ParticipantIdentities;
        var targetIdentity = !string.IsNullOrWhiteSpace(targetPuuid)
            ? identities.FirstOrDefault(i =>
                string.Equals(i.Player.Puuid, targetPuuid, StringComparison.OrdinalIgnoreCase))
            : identities.Count > 0
                ? identities[0]
                : null;

        var targetParticipantId = targetIdentity?.ParticipantId ?? 1;
        var participants = game.Participants;
        var targetParticipant = participants.FirstOrDefault(p => p.ParticipantId == targetParticipantId)
                                ?? (participants.Count > 0 ? participants[0] : null);

        var stats = targetParticipant?.Stats;
        var items = stats != null
            ? new[] { stats.Item0, stats.Item1, stats.Item2, stats.Item3, stats.Item4, stats.Item5, stats.Item6 }
            : Array.Empty<int>();

        var unifiedParticipants = participants.Select(p =>
        {
            var identity = identities.FirstOrDefault(i => i.ParticipantId == p.ParticipantId);
            var pStats = p.Stats;
            var name = identity != null
                ? !string.IsNullOrWhiteSpace(identity.Player.GameName)
                    ? $"{identity.Player.GameName}#{identity.Player.TagLine}"
                    : identity.Player.SummonerName
                : string.Empty;

            var pItems = pStats != null
                ? [pStats.Item0, pStats.Item1, pStats.Item2, pStats.Item3, pStats.Item4, pStats.Item5, pStats.Item6]
                : Array.Empty<int>();

            var pAugments = new List<int>(4);
            if (pStats != null)
            {
                if (pStats.PlayerAugment1 > 0)
                    pAugments.Add(pStats.PlayerAugment1);
                if (pStats.PlayerAugment2 > 0)
                    pAugments.Add(pStats.PlayerAugment2);
                if (pStats.PlayerAugment3 > 0)
                    pAugments.Add(pStats.PlayerAugment3);
                if (pStats.PlayerAugment4 > 0)
                    pAugments.Add(pStats.PlayerAugment4);
                if (pStats.PlayerAugment5 > 0)
                    pAugments.Add(pStats.PlayerAugment5);
                if (pStats.PlayerAugment6 > 0)
                    pAugments.Add(pStats.PlayerAugment6);
            }

            return new UnifiedParticipant(
                identity?.Player.Puuid ?? string.Empty,
                name,
                p.TeamId,
                p.ChampionId,
                string.Empty,
                pStats?.Win ?? false,
                pStats?.Kills ?? 0,
                pStats?.Deaths ?? 0,
                pStats?.Assists ?? 0,
                pStats?.TotalDamageDealtToChampions ?? 0L,
                pStats?.TotalDamageTaken ?? 0L,
                pStats?.GoldEarned ?? 0,
                p.Spell1Id,
                p.Spell2Id,
                pItems,
                pAugments,
                pStats?.DamageDealtToTurrets ?? 0L,
                pStats?.DamageSelfMitigated ?? 0L,
                pStats?.TimeCCingOthers ?? 0,
                pStats?.GoldSpent ?? 0,
                pStats?.TotalHeal ?? 0L,
                pStats?.TotalUnitsHealed ?? 0,
                VisionScore: pStats?.VisionScore ?? 0,
                TotalMinionsKilled: pStats?.TotalMinionsKilled ?? 0,
                NeutralMinionsKilled: pStats?.NeutralMinionsKilled ?? 0,
                DoubleKills: pStats?.DoubleKills ?? 0,
                TripleKills: pStats?.TripleKills ?? 0,
                QuadraKills: pStats?.QuadraKills ?? 0,
                PentaKills: pStats?.PentaKills ?? 0
            );
        }).ToList();

        var augments = new List<int>(4);
        if (stats != null)
        {
            if (stats.PlayerAugment1 > 0)
                augments.Add(stats.PlayerAugment1);
            if (stats.PlayerAugment2 > 0)
                augments.Add(stats.PlayerAugment2);
            if (stats.PlayerAugment3 > 0)
                augments.Add(stats.PlayerAugment3);
            if (stats.PlayerAugment4 > 0)
                augments.Add(stats.PlayerAugment4);
            if (stats.PlayerAugment5 > 0)
                augments.Add(stats.PlayerAugment5);
            if (stats.PlayerAugment6 > 0)
                augments.Add(stats.PlayerAugment6);
        }

        var result = new UnifiedMatchSummary(
            game.GameId,
            game.GameCreation,
            game.GameDuration,
            game.QueueId,
            game.GameMode,
            game.GameType,
            stats?.Win ?? false,
            targetParticipant?.ChampionId ?? 0,
            stats?.Kills ?? 0,
            stats?.Deaths ?? 0,
            stats?.Assists ?? 0,
            stats?.TotalDamageDealtToChampions ?? 0,
            stats?.TotalDamageTaken ?? 0,
            stats?.GoldEarned ?? 0,
            targetParticipant?.Spell1Id ?? 0,
            targetParticipant?.Spell2Id ?? 0,
            items,
            unifiedParticipants,
            "lcu",
            augments
        );
        return result;
    }

    public static UnifiedMatchSummary SgpToUnified(SgpGameItem item, string? targetPuuid)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(item.Json);

        var json = item.Json;
        var participants = json.Participants;
        var target = !string.IsNullOrWhiteSpace(targetPuuid)
            ? participants.FirstOrDefault(p => string.Equals(p.Puuid, targetPuuid, StringComparison.OrdinalIgnoreCase))
            : participants.Count > 0
                ? participants[0]
                : null;

        target ??= participants.Count > 0 ? participants[0] : null;

        var queueId = ExtractQueueIdFromTags(item.Metadata.Tags);

        var targetItems = target != null
            ? new[] { target.Item0, target.Item1, target.Item2, target.Item3, target.Item4, target.Item5, target.Item6 }
            : Array.Empty<int>();

        var unifiedParticipants = participants.Select(p =>
        {
            var name = !string.IsNullOrWhiteSpace(p.RiotIdGameName)
                ? $"{p.RiotIdGameName}#{p.RiotIdTagline}"
                : p.SummonerName;

            var pItems = new[] { p.Item0, p.Item1, p.Item2, p.Item3, p.Item4, p.Item5, p.Item6 };

            var pAugments = new List<int>(4);
            if (p.PlayerAugment1 > 0)
                pAugments.Add(p.PlayerAugment1);
            if (p.PlayerAugment2 > 0)
                pAugments.Add(p.PlayerAugment2);
            if (p.PlayerAugment3 > 0)
                pAugments.Add(p.PlayerAugment3);
            if (p.PlayerAugment4 > 0)
                pAugments.Add(p.PlayerAugment4);
            if (p.PlayerAugment5 > 0)
                pAugments.Add(p.PlayerAugment5);
            if (p.PlayerAugment6 > 0)
                pAugments.Add(p.PlayerAugment6);

            return new UnifiedParticipant(
                p.Puuid,
                name,
                p.TeamId,
                p.ChampionId,
                string.Empty,
                p.Win,
                p.Kills,
                p.Deaths,
                p.Assists,
                p.TotalDamageDealtToChampions,
                p.TotalDamageTaken,
                p.GoldEarned,
                p.Spell1Id,
                p.Spell2Id,
                pItems,
                pAugments,
                p.DamageDealtToTurrets,
                p.DamageSelfMitigated,
                p.TimeCCingOthers,
                p.GoldSpent,
                p.TotalHeal,
                p.TotalUnitsHealed,
                p.TotalDamageShieldedOnTeammates,
                p.TotalHealsOnTeammates,
                p.VisionScore,
                p.TotalMinionsKilled,
                p.NeutralMinionsKilled,
                p.DoubleKills,
                p.TripleKills,
                p.QuadraKills,
                p.PentaKills,
                (int)Math.Round(p.Challenges?.SnowballsHit ?? 0),
                (int)Math.Round(p.Challenges?.KillsOnRecentlyHealedByAramPack ?? 0),
                (int)Math.Round(p.Challenges?.DragonTakedowns ?? 0),
                (int)Math.Round(p.Challenges?.BaronTakedowns ?? 0),
                (int)Math.Round(p.Challenges?.RiftHeraldTakedowns ?? 0),
                (int)Math.Round(p.Challenges?.VoidMonsterKill ?? 0),
                (int)Math.Round(p.Challenges?.EpicMonsterSteals ?? 0),
                (int)Math.Round(p.Challenges?.SoloKills ?? 0),
                (int)Math.Round(p.Challenges?.TurretPlatesTaken ?? 0),
                p.Challenges?.EffectiveHealAndShielding ?? 0,
                p.Challenges?.VisionScorePerMinute ?? 0,
                p.Challenges?.VisionScoreAdvantageLaneOpponent ?? 0,
                p.Challenges?.MaxCsAdvantageOnLaneOpponent ?? 0
            );
        }).ToList();

        var augments = new List<int>(4);
        if (target != null)
        {
            if (target.PlayerAugment1 > 0)
                augments.Add(target.PlayerAugment1);
            if (target.PlayerAugment2 > 0)
                augments.Add(target.PlayerAugment2);
            if (target.PlayerAugment3 > 0)
                augments.Add(target.PlayerAugment3);
            if (target.PlayerAugment4 > 0)
                augments.Add(target.PlayerAugment4);
            if (target.PlayerAugment5 > 0)
                augments.Add(target.PlayerAugment5);
            if (target.PlayerAugment6 > 0)
                augments.Add(target.PlayerAugment6);
        }

        var result = new UnifiedMatchSummary(
            json.GameId,
            json.GameCreation,
            json.GameDuration,
            queueId,
            json.GameMode,
            json.GameType,
            target?.Win ?? false,
            target?.ChampionId ?? 0,
            target?.Kills ?? 0,
            target?.Deaths ?? 0,
            target?.Assists ?? 0,
            target?.TotalDamageDealtToChampions ?? 0,
            target?.TotalDamageTaken ?? 0,
            target?.GoldEarned ?? 0,
            target?.Spell1Id ?? 0,
            target?.Spell2Id ?? 0,
            targetItems,
            unifiedParticipants,
            "sgp",
            augments
        );
        return result;
    }

    public static UnifiedMatchDetails LcuTimelineToUnified(long gameId, LcuGameTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        var frames = timeline.Frames.Select(f =>
        {
            var events = (f.Events ?? []).Select(e => new UnifiedTimelineEvent(
                e.Type,
                e.Timestamp,
                e.ParticipantId,
                e.ItemId,
                e.KillerId,
                e.VictimId,
                e.AssistingParticipantIds ?? []
            )).ToList();

            return new UnifiedTimelineFrame(f.Timestamp, events);
        }).ToList();

        return new UnifiedMatchDetails(gameId, frames, "lcu");
    }

    public static UnifiedMatchDetails SgpDetailsToUnified(SgpGameDetailsLol details)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(details.Json);

        var json = details.Json;
        var frames = json.Frames.Select(f =>
        {
            var events = (f.Events ?? []).Select(e => new UnifiedTimelineEvent(
                e.Type,
                e.Timestamp,
                e.ParticipantId,
                e.ItemId,
                e.KillerId,
                e.VictimId,
                e.AssistingParticipantIds ?? []
            )).ToList();

            return new UnifiedTimelineFrame(f.Timestamp, events);
        }).ToList();

        return new UnifiedMatchDetails(json.GameId, frames, "sgp");
    }

    public static IReadOnlyList<UnifiedTeamMember> GsmToUnified(SgpGsmLedgeRegion gsm)
    {
        ArgumentNullException.ThrowIfNull(gsm);
        ArgumentNullException.ThrowIfNull(gsm.Game);

        var result = new List<UnifiedTeamMember>();
        if (gsm.Game.TeamOne.Count > 0)
            foreach (var p in gsm.Game.TeamOne)
                result.Add(new UnifiedTeamMember(
                    p.Puuid,
                    p.SummonerId,
                    p.FormattedName,
                    p.TeamParticipantId,
                    p.ChampionId,
                    100
                ));

        if (gsm.Game.TeamTwo.Count > 0)
            foreach (var p in gsm.Game.TeamTwo)
                result.Add(new UnifiedTeamMember(
                    p.Puuid,
                    p.SummonerId,
                    p.FormattedName,
                    p.TeamParticipantId,
                    p.ChampionId,
                    200
                ));

        return result;
    }

    private static int ExtractQueueIdFromTags(IReadOnlyList<string>? tags)
    {
        if (tags == null)
            return 0;
        foreach (var tag in tags)
            if (tag.StartsWith("q_", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(tag.AsSpan(2), out var qId))
                return qId;

        return 0;
    }
}