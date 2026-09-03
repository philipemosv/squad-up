namespace SquadUp.Profile.Application;

public sealed record GameCatalogDto(string GameId, string Name);

public sealed record RankTierCatalogDto(string TierId, string Name, int Ordinal);
