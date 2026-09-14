using System.Text.Json.Serialization;
using last.Core.State.Models;

namespace last.Core.Automation;

public sealed record AutoPlayAgainSettings
{
    public bool Enabled { get; init; }

    public bool AutoSearchMatch { get; init; } = true;

    public int PreEndOfGameDelayMs { get; init; } = 3250;

    public int EndOfGameDelayMs { get; init; } = 1575;

    public int WaitingForStatsDelayMs { get; init; } = 10000;

    public bool AutoSkipCelebrations { get; init; } = true;
}

public sealed record AutoPlayAgainExecutionResult(
    [property: JsonPropertyName("success")]
    bool Success,
    [property: JsonPropertyName("triggerPhase")]
    GameflowPhase TriggerPhase,
    [property: JsonPropertyName("matchSearchTriggered")]
    bool MatchSearchTriggered = false,
    [property: JsonPropertyName("errorMessage")]
    string? ErrorMessage = null);