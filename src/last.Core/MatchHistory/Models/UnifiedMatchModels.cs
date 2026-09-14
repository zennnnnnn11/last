using System.Text.Json.Serialization;

namespace last.Core.MatchHistory.Models;

public sealed record UnifiedMatchSummary(
    [property: JsonPropertyName("gameId")] long GameId,
    [property: JsonPropertyName("gameCreation")]
    long GameCreation,
    [property: JsonPropertyName("gameDuration")]
    int GameDuration,
    [property: JsonPropertyName("queueId")]
    int QueueId,
    [property: JsonPropertyName("gameMode")]
    string GameMode,
    [property: JsonPropertyName("gameType")]
    string GameType,
    [property: JsonPropertyName("isWin")] bool IsWin,
    [property: JsonPropertyName("championId")]
    int ChampionId,
    [property: JsonPropertyName("kills")] int Kills,
    [property: JsonPropertyName("deaths")] int Deaths,
    [property: JsonPropertyName("assists")]
    int Assists,
    [property: JsonPropertyName("totalDamageDealtToChampions")]
    long TotalDamageDealtToChampions,
    [property: JsonPropertyName("totalDamageTaken")]
    long TotalDamageTaken,
    [property: JsonPropertyName("goldEarned")]
    int GoldEarned,
    [property: JsonPropertyName("spell1Id")]
    int Spell1Id,
    [property: JsonPropertyName("spell2Id")]
    int Spell2Id,
    [property: JsonPropertyName("items")] IReadOnlyList<int> Items,
    [property: JsonPropertyName("participants")]
    IReadOnlyList<UnifiedParticipant> Participants,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("augments")]
    IReadOnlyList<int>? Augments = null
)
{
    [JsonIgnore] public IReadOnlyList<int> SafeAugments => Augments ?? [];

    [JsonIgnore] public double Kda => Deaths == 0 ? Kills + Assists : Math.Round((double)(Kills + Assists) / Deaths, 2);
}

public sealed record UnifiedParticipant(
    [property: JsonPropertyName("puuid")] string Puuid,
    [property: JsonPropertyName("summonerName")]
    string SummonerName,
    [property: JsonPropertyName("teamId")] int TeamId,
    [property: JsonPropertyName("championId")]
    int ChampionId,
    [property: JsonPropertyName("championName")]
    string ChampionName,
    [property: JsonPropertyName("isWin")] bool IsWin,
    [property: JsonPropertyName("kills")] int Kills,
    [property: JsonPropertyName("deaths")] int Deaths,
    [property: JsonPropertyName("assists")]
    int Assists,
    [property: JsonPropertyName("totalDamageDealtToChampions")]
    long TotalDamageDealtToChampions,
    [property: JsonPropertyName("totalDamageTaken")]
    long TotalDamageTaken,
    [property: JsonPropertyName("goldEarned")]
    int GoldEarned,
    [property: JsonPropertyName("spell1Id")]
    int Spell1Id,
    [property: JsonPropertyName("spell2Id")]
    int Spell2Id,
    [property: JsonPropertyName("items")] IReadOnlyList<int> Items,
    [property: JsonPropertyName("augments")]
    IReadOnlyList<int>? Augments = null,
    [property: JsonPropertyName("damageDealtToTurrets")]
    long DamageDealtToTurrets = 0,
    [property: JsonPropertyName("damageSelfMitigated")]
    long DamageSelfMitigated = 0,
    [property: JsonPropertyName("timeCCingOthers")]
    int TimeCCingOthers = 0,
    [property: JsonPropertyName("goldSpent")]
    int GoldSpent = 0,
    [property: JsonPropertyName("totalHeal")]
    long TotalHeal = 0,
    [property: JsonPropertyName("totalUnitsHealed")]
    int TotalUnitsHealed = 0,
    [property: JsonPropertyName("totalDamageShieldedOnTeammates")]
    long TotalDamageShieldedOnTeammates = 0,
    [property: JsonPropertyName("totalHealsOnTeammates")]
    long TotalHealsOnTeammates = 0,
    [property: JsonPropertyName("visionScore")]
    int VisionScore = 0,
    [property: JsonPropertyName("totalMinionsKilled")]
    int TotalMinionsKilled = 0,
    [property: JsonPropertyName("neutralMinionsKilled")]
    int NeutralMinionsKilled = 0,
    [property: JsonPropertyName("doubleKills")]
    int DoubleKills = 0,
    [property: JsonPropertyName("tripleKills")]
    int TripleKills = 0,
    [property: JsonPropertyName("quadraKills")]
    int QuadraKills = 0,
    [property: JsonPropertyName("pentaKills")]
    int PentaKills = 0,
    [property: JsonPropertyName("snowballsHit")]
    int SnowballsHit = 0,
    [property: JsonPropertyName("killsOnRecentlyHealedByAramPack")]
    int KillsOnRecentlyHealedByAramPack = 0,
    [property: JsonPropertyName("dragonTakedowns")]
    int DragonTakedowns = 0,
    [property: JsonPropertyName("baronTakedowns")]
    int BaronTakedowns = 0,
    [property: JsonPropertyName("riftHeraldTakedowns")]
    int RiftHeraldTakedowns = 0,
    [property: JsonPropertyName("voidMonsterKill")]
    int VoidMonsterKill = 0,
    [property: JsonPropertyName("epicMonsterSteals")]
    int EpicMonsterSteals = 0,
    [property: JsonPropertyName("soloKills")]
    int SoloKills = 0,
    [property: JsonPropertyName("turretPlatesTaken")]
    int TurretPlatesTaken = 0,
    [property: JsonPropertyName("effectiveHealAndShielding")]
    double EffectiveHealAndShielding = 0,
    [property: JsonPropertyName("visionScorePerMinute")]
    double VisionScorePerMinute = 0,
    [property: JsonPropertyName("visionScoreAdvantageLaneOpponent")]
    double VisionScoreAdvantageLaneOpponent = 0,
    [property: JsonPropertyName("maxCsAdvantageOnLaneOpponent")]
    double MaxCsAdvantageOnLaneOpponent = 0
)
{
    [JsonIgnore] public IReadOnlyList<int> SafeAugments => Augments ?? [];
    [JsonIgnore] public double Kda => Deaths == 0 ? Kills + Assists : Math.Round((double)(Kills + Assists) / Deaths, 2);
    [JsonIgnore] public int Cs => TotalMinionsKilled + NeutralMinionsKilled;
}

public sealed record UnifiedMatchDetails(
    [property: JsonPropertyName("gameId")] long GameId,
    [property: JsonPropertyName("frames")] IReadOnlyList<UnifiedTimelineFrame> Frames,
    [property: JsonPropertyName("source")] string Source
);

public sealed record UnifiedTimelineFrame(
    [property: JsonPropertyName("timestamp")]
    int Timestamp,
    [property: JsonPropertyName("events")] IReadOnlyList<UnifiedTimelineEvent> Events
);

public sealed record UnifiedTimelineEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("timestamp")]
    int Timestamp,
    [property: JsonPropertyName("participantId")]
    int ParticipantId,
    [property: JsonPropertyName("itemId")] int ItemId,
    [property: JsonPropertyName("killerId")]
    int KillerId,
    [property: JsonPropertyName("victimId")]
    int VictimId,
    [property: JsonPropertyName("assistingParticipantIds")]
    IReadOnlyList<int> AssistingParticipantIds
);

public sealed record UnifiedTeamMember(
    [property: JsonPropertyName("puuid")] string Puuid,
    [property: JsonPropertyName("summonerId")]
    long SummonerId,
    [property: JsonPropertyName("summonerName")]
    string SummonerName,
    [property: JsonPropertyName("cellId")] int CellId,
    [property: JsonPropertyName("championId")]
    int ChampionId,
    [property: JsonPropertyName("team")] int Team
);