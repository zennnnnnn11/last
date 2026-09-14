using System.Text.Json.Serialization;

namespace last.Core.State.Models;

public sealed record ChampionSimple(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name = "",
    [property: JsonPropertyName("alias")] string Alias = "",
    [property: JsonPropertyName("squarePortraitPath")]
    string SquarePortraitPath = "",
    [property: JsonPropertyName("roles")] IReadOnlyList<string>? Roles = null);