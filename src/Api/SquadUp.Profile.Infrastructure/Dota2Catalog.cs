namespace SquadUp.Profile.Infrastructure;

internal static class Dota2Catalog
{
    public const string GameId = "dota2";
    public const string GameName = "Dota 2";

    public static readonly IReadOnlyList<(string TierId, string Name, int Ordinal)> RankTiers =
    [
        ("herald", "Herald", 1),
        ("guardian", "Guardian", 2),
        ("crusader", "Crusader", 3),
        ("archon", "Archon", 4),
        ("legend", "Legend", 5),
        ("ancient", "Ancient", 6),
        ("divine", "Divine", 7),
        ("immortal", "Immortal", 8)
    ];
}
