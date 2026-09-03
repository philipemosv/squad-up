using Microsoft.EntityFrameworkCore;
using SquadUp.Profile.Application;

namespace SquadUp.Profile.Infrastructure;

internal sealed class GameCatalogQueryService(ProfileDbContext context) : IGameCatalogQueryService
{
    public async Task<IReadOnlyList<GameCatalogDto>> ListGamesAsync(CancellationToken cancellationToken) =>
        await context.Games
            .Where(game => game.IsActive)
            .OrderBy(game => game.Name)
            .Select(game => new GameCatalogDto(game.Id, game.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RankTierCatalogDto>> ListRankTiersAsync(
        string gameId,
        CancellationToken cancellationToken)
    {
        var normalizedGameId = gameId?.Trim().ToLowerInvariant() ?? string.Empty;
        return await context.RankTiers
            .Where(tier => tier.GameId == normalizedGameId && tier.IsActive)
            .OrderBy(tier => tier.Ordinal)
            .Select(tier => new RankTierCatalogDto(tier.TierId, tier.Name, tier.Ordinal))
            .ToListAsync(cancellationToken);
    }
}
