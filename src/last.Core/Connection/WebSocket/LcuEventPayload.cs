using System.Text.Json;
using System.Text.Json.Serialization;

namespace last.Core.Connection.WebSocket;

public sealed record LcuEventPayload(
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("eventType")]
    string EventType,
    [property: JsonPropertyName("data")] JsonElement Data);