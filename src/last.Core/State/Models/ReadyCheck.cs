using System.Text.Json.Serialization;

namespace last.Core.State.Models;

public sealed record ReadyCheck(
    [property: JsonPropertyName("declinerIds")]
    IReadOnlyList<long>? DeclinerIds = null,
    [property: JsonPropertyName("dodgeWarning")]
    string DodgeWarning = "",
    [property: JsonPropertyName("playerResponse")]
    string PlayerResponse = "None",
    [property: JsonPropertyName("state")] string State = "",
    [property: JsonPropertyName("suppressUx")]
    bool SuppressUx = false,
    [property: JsonPropertyName("timer")] double Timer = 0.0
)
{
    [JsonIgnore]
    public bool IsAccepted => string.Equals(PlayerResponse, "Accepted", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsDeclined => string.Equals(PlayerResponse, "Declined", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore] public bool IsInProgress => string.Equals(State, "InProgress", StringComparison.OrdinalIgnoreCase);
}