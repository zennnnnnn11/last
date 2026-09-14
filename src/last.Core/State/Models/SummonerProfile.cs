using System.Text.Json.Serialization;

namespace last.Core.State.Models;

/// <summary>
///     召唤师资料背景装饰，严格对照 LeagueAkari SummonerProfile 定义。
/// </summary>
public sealed record SummonerProfile(
    [property: JsonPropertyName("backgroundSkinAugments")]
    string? BackgroundSkinAugments = null,
    [property: JsonPropertyName("backgroundSkinId")]
    int BackgroundSkinId = 0,
    [property: JsonPropertyName("regalia")]
    string? Regalia = null
);