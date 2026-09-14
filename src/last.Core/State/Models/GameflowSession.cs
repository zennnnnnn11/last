using System.Text.Json.Serialization;

namespace last.Core.State.Models;

public sealed record GameflowSession(
    [property: JsonPropertyName("phase")] GameflowPhase Phase = GameflowPhase.None,
    [property: JsonPropertyName("gameData")]
    GameflowGameData? GameData = null,
    [property: JsonPropertyName("gameClient")]
    GameflowGameClient? GameClient = null,
    [property: JsonPropertyName("gameDodge")]
    GameflowGameDodge? GameDodge = null,
    [property: JsonPropertyName("map")] GameflowMap? Map = null
)
{
    public bool Equals(GameflowSession? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null)
            return false;

        if (Phase != other.Phase)
            return false;

        if (!Equals(GameDodge, other.GameDodge))
            return false;

        if (!Equals(GameClient, other.GameClient))
            return false;

        if (!Equals(Map, other.Map))
            return false;

        if (ReferenceEquals(GameData, other.GameData))
            return true;

        if (GameData is null || other.GameData is null)
            return GameData is null && other.GameData is null;

        return GameData.Equals(other.GameData);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Phase, GameData?.GameId ?? 0, GameDodge?.Phase, GameDodge?.State);
    }
}

public sealed record GameflowGameData(
    [property: JsonPropertyName("gameId")] long GameId = 0,
    [property: JsonPropertyName("gameName")]
    string GameName = "",
    [property: JsonPropertyName("isCustomGame")]
    bool IsCustomGame = false,
    [property: JsonPropertyName("password")]
    string Password = "",
    [property: JsonPropertyName("playerChampionSelections")]
    IReadOnlyList<GameflowPlayerChampionSelection>? PlayerChampionSelections = null,
    [property: JsonPropertyName("queue")] GameflowQueue? Queue = null,
    [property: JsonPropertyName("spectatorKey")]
    string SpectatorKey = "",
    [property: JsonPropertyName("spectatorsAllowed")]
    bool SpectatorsAllowed = false,
    [property: JsonPropertyName("teamOne")]
    IReadOnlyList<GameflowTeamPlayer>? TeamOne = null,
    [property: JsonPropertyName("teamTwo")]
    IReadOnlyList<GameflowTeamPlayer>? TeamTwo = null
)
{
    public bool Equals(GameflowGameData? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null)
            return false;

        if (GameId != other.GameId || IsCustomGame != other.IsCustomGame)
            return false;

        return AreTeamsEqual(TeamOne, other.TeamOne) &&
               AreTeamsEqual(TeamTwo, other.TeamTwo) &&
               AreSelectionsEqual(PlayerChampionSelections, other.PlayerChampionSelections);
    }

    private static bool AreTeamsEqual(IReadOnlyList<GameflowTeamPlayer>? a, IReadOnlyList<GameflowTeamPlayer>? b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a is null || b is null)
            return a is null && b is null;
        if (a.Count != b.Count)
            return false;

        for (var i = 0; i < a.Count; i++)
        {
            var p1 = a[i];
            var p2 = b[i];
            if (p1.ChampionId != p2.ChampionId ||
                !string.Equals(p1.Puuid, p2.Puuid, StringComparison.Ordinal) ||
                !string.Equals(p1.RiotIdGameName, p2.RiotIdGameName, StringComparison.Ordinal) ||
                !string.Equals(p1.RiotIdTagLine, p2.RiotIdTagLine, StringComparison.Ordinal) ||
                !string.Equals(p1.SelectedPosition, p2.SelectedPosition, StringComparison.Ordinal) ||
                !string.Equals(p1.SelectedRole, p2.SelectedRole, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static bool AreSelectionsEqual(
        IReadOnlyList<GameflowPlayerChampionSelection>? a,
        IReadOnlyList<GameflowPlayerChampionSelection>? b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a is null || b is null)
            return a is null && b is null;
        if (a.Count != b.Count)
            return false;

        for (var i = 0; i < a.Count; i++)
        {
            var s1 = a[i];
            var s2 = b[i];
            if (s1.ChampionId != s2.ChampionId ||
                s1.Spell1Id != s2.Spell1Id ||
                s1.Spell2Id != s2.Spell2Id ||
                !string.Equals(s1.Puuid, s2.Puuid, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        return GameId.GetHashCode();
    }
}

public sealed record GameflowPlayerChampionSelection(
    [property: JsonPropertyName("championId")]
    int ChampionId = 0,
    [property: JsonPropertyName("puuid")] string Puuid = "",
    [property: JsonPropertyName("selectedSkinIndex")]
    int SelectedSkinIndex = 0,
    [property: JsonPropertyName("spell1Id")]
    int Spell1Id = 0,
    [property: JsonPropertyName("spell2Id")]
    int Spell2Id = 0
);

public sealed record GameflowTeamPlayer(
    [property: JsonPropertyName("accountId")]
    long AccountId = 0,
    [property: JsonPropertyName("championId")]
    int ChampionId = 0,
    [property: JsonPropertyName("gameName")]
    string GameName = "",
    [property: JsonPropertyName("internalName")]
    string InternalName = "",
    [property: JsonPropertyName("isBot")] bool IsBot = false,
    [property: JsonPropertyName("isHumanoid")]
    bool IsHumanoid = false,
    [property: JsonPropertyName("isLeader")]
    bool IsLeader = false,
    [property: JsonPropertyName("lastSelectedSkinIndex")]
    int LastSelectedSkinIndex = 0,
    [property: JsonPropertyName("playerAlias")]
    string PlayerAlias = "",
    [property: JsonPropertyName("profileIconId")]
    int ProfileIconId = 0,
    [property: JsonPropertyName("puuid")] string Puuid = "",
    [property: JsonPropertyName("riotId")] string RiotId = "",
    [property: JsonPropertyName("riotIdGameName")]
    string RiotIdGameName = "",
    [property: JsonPropertyName("riotIdTagLine")]
    string RiotIdTagLine = "",
    [property: JsonPropertyName("selectedPosition")]
    string SelectedPosition = "",
    [property: JsonPropertyName("selectedRole")]
    string SelectedRole = "",
    [property: JsonPropertyName("summonerId")]
    long SummonerId = 0,
    [property: JsonPropertyName("summonerInternalName")]
    string SummonerInternalName = "",
    [property: JsonPropertyName("summonerLevel")]
    int SummonerLevel = 0,
    [property: JsonPropertyName("summonerName")]
    string SummonerName = "",
    [property: JsonPropertyName("tagLine")]
    string TagLine = "",
    [property: JsonPropertyName("teamOwner")]
    bool TeamOwner = false,
    [property: JsonPropertyName("teamParticipantId")]
    int TeamParticipantId = 0
)
{
    [JsonIgnore]
    public string FormattedName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(GameName) && !string.IsNullOrWhiteSpace(TagLine))
                return $"{GameName}#{TagLine}";
            if (!string.IsNullOrWhiteSpace(RiotIdGameName) && !string.IsNullOrWhiteSpace(RiotIdTagLine))
                return $"{RiotIdGameName}#{RiotIdTagLine}";
            if (!string.IsNullOrWhiteSpace(RiotId))
                return RiotId;
            if (!string.IsNullOrWhiteSpace(GameName))
                return GameName;
            if (!string.IsNullOrWhiteSpace(SummonerName))
                return SummonerName;
            if (!string.IsNullOrWhiteSpace(PlayerAlias))
                return PlayerAlias;
            return string.Empty;
        }
    }
}

public sealed record GameflowQueue(
    [property: JsonPropertyName("id")] int Id = 0,
    [property: JsonPropertyName("name")] string Name = "",
    [property: JsonPropertyName("shortName")]
    string ShortName = "",
    [property: JsonPropertyName("description")]
    string Description = "",
    [property: JsonPropertyName("detailedDescription")]
    string DetailedDescription = "",
    [property: JsonPropertyName("gameMode")]
    string GameMode = "",
    [property: JsonPropertyName("mapId")] int MapId = 0,
    [property: JsonPropertyName("isRanked")]
    bool IsRanked = false,
    [property: JsonPropertyName("isCustom")]
    bool IsCustom = false,
    [property: JsonPropertyName("type")] string Type = "",
    [property: JsonPropertyName("numPlayersPerTeam")]
    int NumPlayersPerTeam = 0,
    [property: JsonPropertyName("minLevel")]
    int MinLevel = 0
);

public sealed record GameflowMap(
    [property: JsonPropertyName("id")] int Id = 0,
    [property: JsonPropertyName("name")] string Name = "",
    [property: JsonPropertyName("gameMode")]
    string GameMode = "",
    [property: JsonPropertyName("gameModeName")]
    string GameModeName = "",
    [property: JsonPropertyName("gameModeShortName")]
    string GameModeShortName = "",
    [property: JsonPropertyName("mapStringId")]
    string MapStringId = "",
    [property: JsonPropertyName("platformId")]
    string PlatformId = "",
    [property: JsonPropertyName("platformName")]
    string PlatformName = ""
);

public sealed record GameflowGameDodge(
    [property: JsonPropertyName("phase")] string Phase = "",
    [property: JsonPropertyName("state")] string State = ""
);

public sealed record GameflowGameClient(
    [property: JsonPropertyName("observerServerIp")]
    string ObserverServerIp = "",
    [property: JsonPropertyName("observerServerPort")]
    int ObserverServerPort = 0,
    [property: JsonPropertyName("running")]
    bool Running = false,
    [property: JsonPropertyName("serverIp")]
    string ServerIp = "",
    [property: JsonPropertyName("serverPort")]
    int ServerPort = 0,
    [property: JsonPropertyName("visible")]
    bool Visible = false
);