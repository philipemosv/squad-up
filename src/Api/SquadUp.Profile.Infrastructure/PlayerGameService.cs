using Microsoft.EntityFrameworkCore;
using SquadUp.Profile.Application;
using SquadUp.Profile.Domain;

namespace SquadUp.Profile.Infrastructure;

internal sealed class PlayerGameService(ProfileDbContext context) : IPlayerGameService
{
    public async Task<IReadOnlyList<PlayerGameDto>> ListAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var rows = await (
            from playerGame in context.PlayerGames
            where playerGame.PlayerId == playerId
            join game in context.Games on playerGame.GameId equals game.Id
            join rankTier in context.RankTiers
                on new { playerGame.GameId, RankTierId = playerGame.RankTierId }
                equals new { rankTier.GameId, RankTierId = rankTier.TierId }
            orderby game.Name
            select new PlayerGameDto(
                game.Id,
                game.Name,
                rankTier.TierId,
                rankTier.Name,
                rankTier.Ordinal,
                playerGame.Region,
                playerGame.VerifiedAtUtc))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<PlayerGameMutationResult> UpsertAsync(
        Guid playerId,
        string gameId,
        PutPlayerGameRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedGameId = gameId?.Trim().ToLowerInvariant() ?? string.Empty;
        var game = await context.Games
            .FirstOrDefaultAsync(candidate => candidate.Id == normalizedGameId && candidate.IsActive, cancellationToken);
        if (game is null)
        {
            return PlayerGameMutationResult.Failed(
                PlayerGameMutationOutcome.GameNotFound,
                $"Game '{normalizedGameId}' is not in the catalog.");
        }

        var normalizedTierId = request.RankTierId?.Trim().ToLowerInvariant() ?? string.Empty;
        var rankTier = await context.RankTiers.FirstOrDefaultAsync(
            candidate => candidate.GameId == normalizedGameId && candidate.TierId == normalizedTierId && candidate.IsActive,
            cancellationToken);
        if (rankTier is null)
        {
            return PlayerGameMutationResult.Failed(
                PlayerGameMutationOutcome.RankTierNotFound,
                $"Rank tier '{normalizedTierId}' is not in the catalog for game '{normalizedGameId}'.");
        }

        var profileExists = await context.PlayerProfiles
            .AnyAsync(profile => profile.PlayerId == playerId, cancellationToken);
        if (!profileExists)
        {
            return PlayerGameMutationResult.Failed(
                PlayerGameMutationOutcome.ValidationFailed,
                "A profile must exist before games can be added.");
        }

        var existing = await context.PlayerGames.FirstOrDefaultAsync(
            candidate => candidate.PlayerId == playerId && candidate.GameId == normalizedGameId,
            cancellationToken);

        try
        {
            if (existing is null)
            {
                context.PlayerGames.Add(new PlayerGame(playerId, rankTier, request.Region));
            }
            else
            {
                existing.UpdateRank(rankTier, request.Region);
            }
        }
        catch (ArgumentException exception)
        {
            return PlayerGameMutationResult.Failed(PlayerGameMutationOutcome.ValidationFailed, exception.Message);
        }

        await context.SaveChangesAsync(cancellationToken);

        return PlayerGameMutationResult.Success(new PlayerGameDto(
            game.Id,
            game.Name,
            rankTier.TierId,
            rankTier.Name,
            rankTier.Ordinal,
            existing?.Region ?? request.Region.Trim().ToUpperInvariant(),
            existing?.VerifiedAtUtc));
    }

    public async Task<PlayerGameRemovalOutcome> RemoveAsync(
        Guid playerId,
        string gameId,
        CancellationToken cancellationToken)
    {
        var normalizedGameId = gameId?.Trim().ToLowerInvariant() ?? string.Empty;
        var existing = await context.PlayerGames.FirstOrDefaultAsync(
            candidate => candidate.PlayerId == playerId && candidate.GameId == normalizedGameId,
            cancellationToken);
        if (existing is null)
        {
            return PlayerGameRemovalOutcome.NotFound;
        }

        context.PlayerGames.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
        return PlayerGameRemovalOutcome.Removed;
    }
}
