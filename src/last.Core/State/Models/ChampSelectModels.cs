using System.Text.Json.Serialization;

namespace last.Core.State.Models;

public sealed record ChampSelectSession(
    [property: JsonPropertyName("actions")]
    IReadOnlyList<IReadOnlyList<ChampSelectAction>>? Actions = null,
    [property: JsonPropertyName("allowBattleBoost")]
    bool AllowBattleBoost = false,
    [property: JsonPropertyName("allowDuplicatePicks")]
    bool AllowDuplicatePicks = false,
    [property: JsonPropertyName("allowLockedEvents")]
    bool AllowLockedEvents = false,
    [property: JsonPropertyName("allowPlayerPickSameChampion")]
    bool AllowPlayerPickSameChampion = false,
    [property: JsonPropertyName("allowSkinSelection")]
    bool AllowSkinSelection = false,
    [property: JsonPropertyName("allowSubsetChampionPicks")]
    bool AllowSubsetChampionPicks = false,
    [property: JsonPropertyName("benchChampions")]
    IReadOnlyList<BenchChampion>? BenchChampions = null,
    [property: JsonPropertyName("benchEnabled")]
    bool BenchEnabled = false,
    [property: JsonPropertyName("boostableSkinCount")]
    int BoostableSkinCount = 0,
    [property: JsonPropertyName("counter")]
    int Counter = 0,
    [property: JsonPropertyName("disallowBanningTeammateHoveredChampions")]
    bool DisallowBanningTeammateHoveredChampions = false,
    [property: JsonPropertyName("gameId")] long GameId = 0,
    [property: JsonPropertyName("hasSimultaneousBans")]
    bool HasSimultaneousBans = false,
    [property: JsonPropertyName("hasSimultaneousPicks")]
    bool HasSimultaneousPicks = false,
    [property: JsonPropertyName("id")] string Id = "",
    [property: JsonPropertyName("isCustomGame")]
    bool IsCustomGame = false,
    [property: JsonPropertyName("isLegacyChampSelect")]
    bool IsLegacyChampSelect = false,
    [property: JsonPropertyName("isSpectating")]
    bool IsSpectating = false,
    [property: JsonPropertyName("localPlayerCellId")]
    int LocalPlayerCellId = 0,
    [property: JsonPropertyName("lockedEventIndex")]
    int LockedEventIndex = 0,
    [property: JsonPropertyName("myTeam")] IReadOnlyList<ChampSelectTeamPlayer>? MyTeam = null,
    [property: JsonPropertyName("queueId")]
    int QueueId = 0,
    [property: JsonPropertyName("showQuitButton")]
    bool ShowQuitButton = false,
    [property: JsonPropertyName("skipChampionSelect")]
    bool SkipChampionSelect = false,
    [property: JsonPropertyName("theirTeam")]
    IReadOnlyList<ChampSelectTeamPlayer>? TheirTeam = null,
    [property: JsonPropertyName("timer")] ChampSelectTimer? Timer = null
)
{
    [JsonIgnore] public bool IsBenchEnabled => BenchEnabled && BenchChampions is not null;

    [JsonIgnore]
    public ChampSelectTeamPlayer? LocalPlayer
    {
        get
        {
            if (MyTeam is null)
                return null;

            foreach (var player in MyTeam)
                if (player.CellId == LocalPlayerCellId)
                    return player;

            return null;
        }
    }

    [JsonIgnore] public int LocalPlayerChampionId => LocalPlayer?.ChampionId ?? 0;

    public bool Equals(ChampSelectSession? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null)
            return false;

        // 契约短路比对：在 LCU 规范中，选人会话的 actions、myTeam/theirTeam 英雄与技能选择、
        // 以及板凳席交换等任何内部状态变更都会原子递增 counter。
        // 比对链完整覆盖 Counter、GameId、Id、LocalPlayerCellId 与 IsSpectating，保证状态严格等价。
        return Counter == other.Counter &&
               GameId == other.GameId &&
               string.Equals(Id, other.Id, StringComparison.Ordinal) &&
               LocalPlayerCellId == other.LocalPlayerCellId &&
               IsSpectating == other.IsSpectating;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, GameId, Counter, LocalPlayerCellId);
    }
}

public sealed record BenchChampion(
    [property: JsonPropertyName("championId")]
    int ChampionId = 0,
    [property: JsonPropertyName("isPriority")]
    bool IsPriority = false
);

public sealed record ChampSelectTeamPlayer(
    [property: JsonPropertyName("assignedPosition")]
    string AssignedPosition = "",
    [property: JsonPropertyName("cellId")] int CellId = 0,
    [property: JsonPropertyName("championId")]
    int ChampionId = 0,
    [property: JsonPropertyName("championPickIntent")]
    int ChampionPickIntent = 0,
    [property: JsonPropertyName("gameName")]
    string GameName = "",
    [property: JsonPropertyName("internalName")]
    string InternalName = "",
    [property: JsonPropertyName("isAutofilled")]
    bool IsAutofilled = false,
    [property: JsonPropertyName("isHumanoid")]
    bool IsHumanoid = false,
    [property: JsonPropertyName("nameVisibilityType")]
    string NameVisibilityType = "",
    [property: JsonPropertyName("obfuscatedPuuid")]
    string ObfuscatedPuuid = "",
    [property: JsonPropertyName("obfuscatedSummonerId")]
    long ObfuscatedSummonerId = 0,
    [property: JsonPropertyName("pickMode")]
    int PickMode = 0,
    [property: JsonPropertyName("pickTurn")]
    int PickTurn = 0,
    [property: JsonPropertyName("playerAlias")]
    string PlayerAlias = "",
    [property: JsonPropertyName("playerType")]
    string PlayerType = "",
    [property: JsonPropertyName("puuid")] string Puuid = "",
    [property: JsonPropertyName("selectedSkinId")]
    int SelectedSkinId = 0,
    [property: JsonPropertyName("spell1Id")]
    long Spell1Id = 0,
    [property: JsonPropertyName("spell2Id")]
    long Spell2Id = 0,
    [property: JsonPropertyName("summonerId")]
    long SummonerId = 0,
    [property: JsonPropertyName("tagLine")]
    string TagLine = "",
    [property: JsonPropertyName("team")] int Team = 0,
    [property: JsonPropertyName("wardSkinId")]
    long WardSkinId = 0
)
{
    [JsonIgnore]
    public string FormattedName =>
        !string.IsNullOrWhiteSpace(GameName) && !string.IsNullOrWhiteSpace(TagLine)
            ? $"{GameName}#{TagLine}"
            : PlayerAlias;
}

public sealed record ChampSelectAction(
    [property: JsonPropertyName("actorCellId")]
    int ActorCellId = 0,
    [property: JsonPropertyName("championId")]
    int ChampionId = 0,
    [property: JsonPropertyName("completed")]
    bool Completed = false,
    [property: JsonPropertyName("duration")]
    double Duration = 0.0,
    [property: JsonPropertyName("id")] long Id = 0,
    [property: JsonPropertyName("isAllyAction")]
    bool IsAllyAction = false,
    [property: JsonPropertyName("isInProgress")]
    bool IsInProgress = false,
    [property: JsonPropertyName("pickTurn")]
    int PickTurn = 0,
    [property: JsonPropertyName("type")] string Type = ""
);

public sealed record ChampSelectTimer(
    [property: JsonPropertyName("adjustedTimeLeftInPhase")]
    long AdjustedTimeLeftInPhase = 0,
    [property: JsonPropertyName("internalNowInEpochMs")]
    long InternalNowInEpochMs = 0,
    [property: JsonPropertyName("isInfinite")]
    bool IsInfinite = false,
    [property: JsonPropertyName("phase")] string Phase = "",
    [property: JsonPropertyName("totalTimeInPhase")]
    long TotalTimeInPhase = 0
);

public sealed record ChampSelectActionUpdateRequest(
    [property: JsonPropertyName("championId")]
    int? ChampionId = null,
    [property: JsonPropertyName("completed")]
    bool? Completed = null,
    [property: JsonPropertyName("type")] string? Type = null
);