namespace SquadUp.LobbyService.Infrastructure;

internal static class LobbyCatalog
{
    internal const string Dota2GameId = "dota2";
    internal const string Dota2GameName = "Dota 2";

    internal static readonly IReadOnlyList<(string TierId, string Name, int Ordinal)> Dota2RankTiers =
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
