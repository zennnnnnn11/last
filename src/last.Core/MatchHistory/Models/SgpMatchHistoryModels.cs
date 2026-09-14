using System.Text.Json.Serialization;

namespace last.Core.MatchHistory.Models;

public sealed record SgpMatchHistoryLol(
    [property: JsonPropertyName("games")] IReadOnlyList<SgpGameItem> Games
);

public sealed record SgpGameSummaryLol(
    [property: JsonPropertyName("metadata")]
    SgpGameMetadata Metadata,
    [property: JsonPropertyName("json")] SgpGameJson Json
);

public sealed record SgpGameItem(
    [property: JsonPropertyName("metadata")]
    SgpGameMetadata Metadata,
    [property: JsonPropertyName("json")] SgpGameJson Json
);

public sealed record SgpGameMetadata(
    [property: JsonPropertyName("product")]
    string Product,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("participants")]
    IReadOnlyList<string> Participants,
    [property: JsonPropertyName("timestamp")]
    string Timestamp,
    [property: JsonPropertyName("data_version")]
    string DataVersion,
    [property: JsonPropertyName("info_type")]
    string InfoType,
    [property: JsonPropertyName("match_id")]
    string MatchId,
    [property: JsonPropertyName("private")]
    bool Private
);

public sealed record SgpGameJson(
    [property: JsonPropertyName("gameId")] long GameId,
    [property: JsonPropertyName("gameCreation")]
    long GameCreation,
    [property: JsonPropertyName("gameDuration")]
    int GameDuration,
    [property: JsonPropertyName("gameMode")]
    string GameMode,
    [property: JsonPropertyName("gameType")]
    string GameType,
    [property: JsonPropertyName("mapId")] int MapId,
    [property: JsonPropertyName("participants")]
    IReadOnlyList<SgpParticipant> Participants
);

public sealed record SgpParticipant(
    [property: JsonPropertyName("puuid")] string Puuid,
    [property: JsonPropertyName("summonerId")]
    long SummonerId,
    [property: JsonPropertyName("riotIdGameName")]
    string RiotIdGameName,
    [property: JsonPropertyName("riotIdTagline")]
    string RiotIdTagline,
    [property: JsonPropertyName("summonerName")]
    string SummonerName,
    [property: JsonPropertyName("championId")]
    int ChampionId,
    [property: JsonPropertyName("teamId")] int TeamId,
    [property: JsonPropertyName("win")] bool Win,
    [property: JsonPropertyName("kills")] int Kills,
    [property: JsonPropertyName("deaths")] int Deaths,
    [property: JsonPropertyName("assists")]
    int Assists,
    [property: JsonPropertyName("kda")] double Kda,
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
    [property: JsonPropertyName("spell1Id")]
    int Spell1Id,
    [property: JsonPropertyName("spell2Id")]
    int Spell2Id,
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
    [property: JsonPropertyName("challenges")]
    SgpParticipantChallenges? Challenges = null
);

public sealed record SgpParticipantChallenges(
    [property: JsonPropertyName("snowballsHit")]
    double SnowballsHit = 0,
    [property: JsonPropertyName("killsOnRecentlyHealedByAramPack")]
    double KillsOnRecentlyHealedByAramPack = 0,
    [property: JsonPropertyName("dragonTakedowns")]
    double DragonTakedowns = 0,
    [property: JsonPropertyName("baronTakedowns")]
    double BaronTakedowns = 0,
    [property: JsonPropertyName("riftHeraldTakedowns")]
    double RiftHeraldTakedowns = 0,
    [property: JsonPropertyName("voidMonsterKill")]
    double VoidMonsterKill = 0,
    [property: JsonPropertyName("epicMonsterSteals")]
    double EpicMonsterSteals = 0,
    [property: JsonPropertyName("soloKills")]
    double SoloKills = 0,
    [property: JsonPropertyName("turretPlatesTaken")]
    double TurretPlatesTaken = 0,
    [property: JsonPropertyName("effectiveHealAndShielding")]
    double EffectiveHealAndShielding = 0,
    [property: JsonPropertyName("visionScorePerMinute")]
    double VisionScorePerMinute = 0,
    [property: JsonPropertyName("visionScoreAdvantageLaneOpponent")]
    double VisionScoreAdvantageLaneOpponent = 0,
    [property: JsonPropertyName("maxCsAdvantageOnLaneOpponent")]
    double MaxCsAdvantageOnLaneOpponent = 0
);

public sealed record SgpGameDetailsLol(
    [property: JsonPropertyName("metadata")]
    SgpGameMetadata Metadata,
    [property: JsonPropertyName("json")] SgpGameDetailsJson Json
);

public sealed record SgpGameDetailsJson(
    [property: JsonPropertyName("gameId")] long GameId,
    [property: JsonPropertyName("frames")] IReadOnlyList<SgpTimelineFrame> Frames
);

public sealed record SgpTimelineFrame(
    [property: JsonPropertyName("timestamp")]
    int Timestamp,
    [property: JsonPropertyName("events")] IReadOnlyList<SgpTimelineEvent>? Events
);

public sealed record SgpTimelineEvent(
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

public sealed record SgpGsmLedgeRegion(
    [property: JsonPropertyName("game")] SgpGsmGame Game
);

public sealed record SgpGsmGame(
    [property: JsonPropertyName("gameId")] long GameId,
    [property: JsonPropertyName("gameMode")]
    string GameMode,
    [property: JsonPropertyName("teamOne")]
    IReadOnlyList<SgpGsmTeamPlayer> TeamOne,
    [property: JsonPropertyName("teamTwo")]
    IReadOnlyList<SgpGsmTeamPlayer> TeamTwo,
    [property: JsonPropertyName("playerChampionSelections")]
    IReadOnlyList<SgpGsmChampionSelection>? PlayerChampionSelections
);

public sealed record SgpGsmTeamPlayer(
    [property: JsonPropertyName("puuid")] string Puuid,
    [property: JsonPropertyName("championId")]
    int ChampionId = 0,
    [property: JsonPropertyName("teamParticipantId")]
    int TeamParticipantId = 0,
    [property: JsonPropertyName("selectedPosition")]
    string SelectedPosition = "",
    [property: JsonPropertyName("selectedRole")]
    string SelectedRole = "",
    [property: JsonPropertyName("summonerId")]
    long SummonerId = 0,
    [property: JsonPropertyName("summonerName")]
    string SummonerName = "",
    [property: JsonPropertyName("gameName")]
    string GameName = "",
    [property: JsonPropertyName("tagLine")]
    string TagLine = ""
)
{
    [JsonIgnore]
    public string FormattedName =>
        !string.IsNullOrWhiteSpace(GameName) && !string.IsNullOrWhiteSpace(TagLine)
            ? $"{GameName}#{TagLine}"
            : !string.IsNullOrWhiteSpace(GameName)
                ? GameName
                : SummonerName;
}

public sealed record SgpGsmChampionSelection(
    [property: JsonPropertyName("puuid")] string Puuid,
    [property: JsonPropertyName("spell1Id")]
    int Spell1Id,
    [property: JsonPropertyName("spell2Id")]
    int Spell2Id
);