using System.Text.Json.Serialization;

namespace last.Core.MatchHistory.Models;

public sealed record LcuMatchHistory(
    [property: JsonPropertyName("accountId")]
    long AccountId,
    [property: JsonPropertyName("games")] LcuGamesContainer Games
);

public sealed record LcuGamesContainer(
    [property: JsonPropertyName("games")] IReadOnlyList<LcuGameSummary> Games
);

public sealed record LcuGameSummary(
    [property: JsonPropertyName("gameId")] long GameId,
    [property: JsonPropertyName("platformId")]
    string PlatformId,
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
    [property: JsonPropertyName("mapId")] int MapId,
    [property: JsonPropertyName("participants")]
    IReadOnlyList<LcuParticipant> Participants,
    [property: JsonPropertyName("participantIdentities")]
    IReadOnlyList<LcuParticipantIdentity> ParticipantIdentities
);

public sealed record LcuParticipant(
    [property: JsonPropertyName("participantId")]
    int ParticipantId,
    [property: JsonPropertyName("teamId")] int TeamId,
    [property: JsonPropertyName("championId")]
    int ChampionId,
    [property: JsonPropertyName("spell1Id")]
    int Spell1Id,
    [property: JsonPropertyName("spell2Id")]
    int Spell2Id,
    [property: JsonPropertyName("stats")] LcuParticipantStats? Stats = null
);

public sealed record LcuParticipantStats(
    [property: JsonPropertyName("win")] bool Win,
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
    [property: JsonPropertyName("item0")] int Item0,
    [property: JsonPropertyName("item1")] int Item1,
    [property: JsonPropertyName("item2")] int Item2,
    [property: JsonPropertyName("item3")] int Item3,
    [property: JsonPropertyName("item4")] int Item4,
    [property: JsonPropertyName("item5")] int Item5,
    [property: JsonPropertyName("item6")] int Item6,
    [property: JsonPropertyName("playerAugment1")]
    int PlayerAugment1 = 0,
    [property: JsonPropertyName("playerAugment2")]
    int PlayerAugment2 = 0,
    [property: JsonPropertyName("playerAugment3")]
    int PlayerAugment3 = 0,
    [property: JsonPropertyName("playerAugment4")]
    int PlayerAugment4 = 0,
    [property: JsonPropertyName("playerAugment5")]
    int PlayerAugment5 = 0,
    [property: JsonPropertyName("playerAugment6")]
    int PlayerAugment6 = 0,
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
    int PentaKills = 0
);

public sealed record LcuParticipantIdentity(
    [property: JsonPropertyName("participantId")]
    int ParticipantId,
    [property: JsonPropertyName("player")] LcuPlayerIdentity Player
);

public sealed record LcuPlayerIdentity(
    [property: JsonPropertyName("puuid")] string Puuid,
    [property: JsonPropertyName("summonerId")]
    long SummonerId,
    [property: JsonPropertyName("summonerName")]
    string SummonerName,
    [property: JsonPropertyName("gameName")]
    string GameName,
    [property: JsonPropertyName("tagLine")]
    string TagLine
);

public sealed record LcuGameTimeline(
    [property: JsonPropertyName("frames")] IReadOnlyList<LcuTimelineFrame> Frames
);

public sealed record LcuTimelineFrame(
    [property: JsonPropertyName("timestamp")]
    int Timestamp,
    [property: JsonPropertyName("events")] IReadOnlyList<LcuTimelineEvent>? Events
);

public sealed record LcuTimelineEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("timestamp")]
    int Timestamp,
    [property: JsonPropertyName("participantId")]
    int ParticipantId = 0,
    [property: JsonPropertyName("itemId")] int ItemId = 0,
    [property: JsonPropertyName("killerId")]
    int KillerId = 0,
    [property: JsonPropertyName("victimId")]
    int VictimId = 0,
    [property: JsonPropertyName("assistingParticipantIds")]
    IReadOnlyList<int>? AssistingParticipantIds = null
);

public sealed record EntitlementsToken(
    [property: JsonPropertyName("accessToken")]
    string AccessToken,
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("entitlements")]
    IReadOnlyList<string> Entitlements
);