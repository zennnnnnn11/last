using System.Text.Json.Serialization;

namespace last.Core.GameData.Static;

public sealed record LcuItemDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("description")]
    string? Description = null,
    [property: JsonPropertyName("active")] bool Active = false,
    [property: JsonPropertyName("inStore")]
    bool InStore = false,
    [property: JsonPropertyName("from")] IReadOnlyList<int>? From = null,
    [property: JsonPropertyName("to")] IReadOnlyList<int>? To = null,
    [property: JsonPropertyName("categories")]
    IReadOnlyList<string>? Categories = null,
    [property: JsonPropertyName("price")] int Price = 0,
    [property: JsonPropertyName("priceTotal")]
    int PriceTotal = 0,
    [property: JsonPropertyName("iconPath")]
    string? IconPath = null
);

public sealed record ItemStaticInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")]
    string Description,
    [property: JsonPropertyName("priceTotal")]
    int PriceTotal,
    [property: JsonPropertyName("from")] IReadOnlyList<int> From,
    [property: JsonPropertyName("to")] IReadOnlyList<int> To,
    [property: JsonPropertyName("categories")]
    IReadOnlyList<string> Categories,
    [property: JsonPropertyName("iconPath")]
    string IconPath
)
{
    [JsonIgnore] public string GtimgIconUri => ItemStaticDataDefaults.GetGtimgIconUri(Id);
}

public static class ItemStaticDataDefaults
{
    public const string GtimgItemBaseUrl = "https://game.gtimg.cn/images/lol/act/img/item";

    public static string GetGtimgIconUri(int itemId)
    {
        return itemId > 0 ? $"{GtimgItemBaseUrl}/{itemId}.png" : string.Empty;
    }
}