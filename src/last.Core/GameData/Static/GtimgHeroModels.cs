using System.Globalization;
using System.Text.Json.Serialization;

namespace last.Core.GameData.Static;

public sealed record GtimgHeroList(
    [property: JsonPropertyName("hero")] IReadOnlyList<GtimgHero>? Hero = null,
    [property: JsonPropertyName("version")]
    string Version = "",
    [property: JsonPropertyName("fileName")]
    string FileName = "",
    [property: JsonPropertyName("fileTime")]
    string FileTime = "");

public sealed record GtimgHero(
    [property: JsonPropertyName("heroId")] string HeroId = "",
    [property: JsonPropertyName("name")] string Name = "",
    [property: JsonPropertyName("alias")] string Alias = "",
    [property: JsonPropertyName("title")] string Title = "",
    [property: JsonPropertyName("roles")] IReadOnlyList<string>? Roles = null,
    [property: JsonPropertyName("keywords")]
    string Keywords = "")
{
    [JsonIgnore] public int NumericHeroId => int.TryParse(HeroId, CultureInfo.InvariantCulture, out var id) ? id : 0;
}