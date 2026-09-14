using System.Text.Json;
using System.Text.Json.Serialization;

namespace last.Core.State.Models;

[JsonConverter(typeof(GameflowPhaseJsonConverter))]
public enum GameflowPhase
{
    None = 0,
    Lobby,
    Matchmaking,
    ReadyCheck,
    ChampSelect,
    GameStart,
    InProgress,
    Reconnect,
    WaitingForStats,
    PreEndOfGame,
    EndOfGame,
    WatchInProgress,
    TerminatedInError,
    Unknown
}

public sealed class GameflowPhaseJsonConverter : JsonConverter<GameflowPhase>
{
    public override GameflowPhase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
                return GameflowPhase.None;

            if (Enum.TryParse<GameflowPhase>(value, true, out var phase))
                return phase;

            return GameflowPhase.Unknown;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var intVal))
            if (Enum.IsDefined(typeof(GameflowPhase), intVal))
                return (GameflowPhase)intVal;

        return GameflowPhase.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, GameflowPhase value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}