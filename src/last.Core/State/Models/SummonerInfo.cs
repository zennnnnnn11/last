using System.Text.Json.Serialization;

namespace last.Core.State.Models;

public sealed record SummonerInfo(
    [property: JsonPropertyName("accountId")]
    long AccountId = 0,
    [property: JsonPropertyName("displayName")]
    string DisplayName = "",
    [property: JsonPropertyName("gameName")]
    string GameName = "",
    [property: JsonPropertyName("internalName")]
    string InternalName = "",
    [property: JsonPropertyName("nameChangeFlag")]
    bool NameChangeFlag = false,
    [property: JsonPropertyName("percentCompleteForNextLevel")]
    int PercentCompleteForNextLevel = 0,
    [property: JsonPropertyName("privacy")]
    string Privacy = "",
    [property: JsonPropertyName("profileIconId")]
    int ProfileIconId = 0,
    [property: JsonPropertyName("puuid")] string Puuid = "",
    [property: JsonPropertyName("tagLine")]
    string TagLine = "",
    [property: JsonPropertyName("summonerId")]
    long SummonerId = 0,
    [property: JsonPropertyName("summonerLevel")]
    int SummonerLevel = 0,
    [property: JsonPropertyName("unnamed")]
    bool Unnamed = false,
    [property: JsonPropertyName("xpSinceLastLevel")]
    long XpSinceLastLevel = 0,
    [property: JsonPropertyName("xpUntilNextLevel")]
    long XpUntilNextLevel = 0
)
{
    [JsonIgnore]
    public string FormattedName =>
        !string.IsNullOrWhiteSpace(GameName) && !string.IsNullOrWhiteSpace(TagLine)
            ? $"{GameName}#{TagLine}"
            : DisplayName;
}