namespace last.Core.GameData.Static;

public sealed record ChampionStaticInfo(
    int Id,
    string Name,
    string Alias,
    string Title,
    IReadOnlyList<string> Roles,
    string SquarePortraitPath);