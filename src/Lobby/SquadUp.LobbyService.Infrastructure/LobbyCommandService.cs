using Microsoft.EntityFrameworkCore;
using SquadUp.LobbyService.Application;
using SquadUp.LobbyService.Domain;

namespace SquadUp.LobbyService.Infrastructure;

internal sealed class LobbyCommandService(LobbyDbContext context) : ILobbyCommandService
{
    public async Task<CreateLobbyResult> CreateAsync(
        Guid ownerPlayerId,
        CreateLobbyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (ownerPlayerId == Guid.Empty)
        {
            return CreateLobbyResult.Failed(
                CreateLobbyOutcome.ValidationFailed,
                "Lobby owner is required.");
        }

        RankRequirement rankRequirement;
        try
        {
            rankRequirement = new RankRequirement(
                request.GameId,
                request.MinimumRankOrdinal,
                request.MaximumRankOrdinal);
        }
        catch (ArgumentException exception)
        {
            return CreateLobbyResult.Failed(CreateLobbyOutcome.ValidationFailed, exception.Message);
        }

        if (request.Capacity is < Lobby.MinimumCapacity or > Lobby.MaximumCapacity)
        {
            return CreateLobbyResult.Failed(
                CreateLobbyOutcome.ValidationFailed,
                $"Capacity must be between {Lobby.MinimumCapacity} and {Lobby.MaximumCapacity}.");
        }

        var gameExists = await context.Set<LobbyCatalogEntry>()
            .AsNoTracking()
            .AnyAsync(
                game => game.Id == rankRequirement.GameId && game.IsActive,
                cancellationToken);
        if (!gameExists)
        {
            return CreateLobbyResult.Failed(CreateLobbyOutcome.GameNotFound, "The requested game is not active.");
        }

        var rankOrdinals = new[] { rankRequirement.MinimumOrdinal, rankRequirement.MaximumOrdinal }
            .Where(ordinal => ordinal.HasValue)
            .Select(ordinal => ordinal!.Value)
            .Distinct()
            .ToArray();
        var activeRankCount = await context.Set<LobbyRankTierEntry>()
            .AsNoTracking()
            .CountAsync(
                tier => tier.GameId == rankRequirement.GameId &&
                    tier.IsActive &&
                    rankOrdinals.Contains(tier.Ordinal),
                cancellationToken);
        if (activeRankCount != rankOrdinals.Length)
        {
            return CreateLobbyResult.Failed(
                CreateLobbyOutcome.RankTierNotFound,
                "Every requested rank ordinal must be active for the selected game.");
        }

        var lobby = new Lobby(Guid.CreateVersion7(), ownerPlayerId, request.Capacity, rankRequirement);
        context.Lobbies.Add(lobby);
        await context.SaveChangesAsync(cancellationToken);

        return CreateLobbyResult.Success(lobby.Id);
    }
}
