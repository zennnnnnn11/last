using System.Text.Json.Serialization;

namespace last.Core.State.Models;

public sealed record SequenceEvent(
    [property: JsonPropertyName("name")] string? Name = null);