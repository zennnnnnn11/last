using System.Text.Json.Serialization;

namespace last.Core.State.Models;

// ReSharper disable NotAccessedPositionalProperty.Global
/// <summary>
///     匹配状态与队列详细信息，严格对齐 LeagueAkari GetSearch 定义。
/// </summary>
public sealed record MatchmakingSearch(
    [property: JsonPropertyName("dodgeData")]
    DodgeData? DodgeData = null,
    [property: JsonPropertyName("errors")] IReadOnlyList<MatchmakingError>? Errors = null,
    [property: JsonPropertyName("estimatedQueueTime")]
    double EstimatedQueueTime = 0.0,
    [property: JsonPropertyName("isCurrentlyInQueue")]
    bool IsCurrentlyInQueue = false,
    [property: JsonPropertyName("lobbyId")]
    string LobbyId = "",
    [property: JsonPropertyName("lowPriorityData")]
    LowPriorityData? LowPriorityData = null,
    [property: JsonPropertyName("queueId")]
    int QueueId = 0,
    [property: JsonPropertyName("readyCheck")]
    ReadyCheck? ReadyCheck = null,
    [property: JsonPropertyName("searchState")]
    string SearchState = "",
    [property: JsonPropertyName("timeInQueue")]
    double TimeInQueue = 0.0
);

/// <summary>
///     秒退惩罚与状态信息。
/// </summary>
public sealed record DodgeData(
    [property: JsonPropertyName("dodgerId")]
    long DodgerId = 0,
    [property: JsonPropertyName("state")] string State = ""
);

/// <summary>
///     低优先级队列惩罚信息。
/// </summary>
public sealed record LowPriorityData(
    [property: JsonPropertyName("bustedLeaverAccessToken")]
    string BustedLeaverAccessToken = "",
    [property: JsonPropertyName("penalizedSummonerIds")]
    IReadOnlyList<long>? PenalizedSummonerIds = null,
    [property: JsonPropertyName("penaltyTime")]
    double PenaltyTime = 0.0,
    [property: JsonPropertyName("penaltyTimeRemaining")]
    double PenaltyTimeRemaining = 0.0,
    [property: JsonPropertyName("reason")] string Reason = ""
);

/// <summary>
///     匹配系统错误信息。
/// </summary>
public sealed record MatchmakingError(
    [property: JsonPropertyName("errorType")]
    string ErrorType = "",
    [property: JsonPropertyName("id")] int Id = 0,
    [property: JsonPropertyName("message")]
    string Message = "",
    [property: JsonPropertyName("penalizedSummonerId")]
    long PenalizedSummonerId = 0,
    [property: JsonPropertyName("penaltyTimeRemaining")]
    double PenaltyTimeRemaining = 0.0
);