using System.Text.Json.Serialization;

namespace last.Core.GameData.Static;

public sealed record GtimgKiwiAugment(
    [property: JsonPropertyName("augmentID")]
    int AugmentId,
    [property: JsonPropertyName("name_cn")]
    string? NameCn = null,
    [property: JsonPropertyName("level")] string? Level = null,
    [property: JsonPropertyName("small_Icon")]
    string? SmallIcon = null,
    [property: JsonPropertyName("desc")] string? Desc = ""
);