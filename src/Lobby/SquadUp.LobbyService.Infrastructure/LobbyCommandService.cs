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

    public async Task<LobbyMembershipResult> JoinAsync(
        Guid lobbyId,
        Guid playerId,
        JoinLobbyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        LobbyMember member;
        try
        {
            member = new LobbyMember(
                playerId,
                request.DiscordUserId,
                request.DisplayName,
                new PlayerRank(request.GameId, request.RankOrdinal));
        }
        catch (ArgumentException exception)
        {
            return LobbyMembershipResult.Failed(LobbyMembershipOutcome.ValidationFailed, exception.Message);
        }

        return await ChangeMembershipAsync(
            lobbyId,
            lobby => lobby.AddMember(member),
            cancellationToken);
    }

    public Task<LobbyMembershipResult> LeaveAsync(
        Guid lobbyId,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        if (playerId == Guid.Empty)
        {
            return Task.FromResult(LobbyMembershipResult.Failed(
                LobbyMembershipOutcome.ValidationFailed,
                "Player id is required."));
        }

        return ChangeMembershipAsync(lobbyId, lobby => lobby.RemoveMember(playerId), cancellationToken);
    }

    private async Task<LobbyMembershipResult> ChangeMembershipAsync(
        Guid lobbyId,
        Action<Lobby> change,
        CancellationToken cancellationToken)
    {
        if (lobbyId == Guid.Empty)
        {
            return LobbyMembershipResult.Failed(LobbyMembershipOutcome.ValidationFailed, "Lobby id is required.");
        }

        const int maximumAttempts = 2;
        for (var attempt = 0; attempt < maximumAttempts; attempt++)
        {
            var lobby = await context.Lobbies
                .Include("members")
                .SingleOrDefaultAsync(candidate => candidate.Id == lobbyId, cancellationToken);
            if (lobby is null)
            {
                return LobbyMembershipResult.Failed(LobbyMembershipOutcome.LobbyNotFound, "Lobby was not found.");
            }

            try
            {
                change(lobby);
            }
            catch (InvalidOperationException exception)
            {
                return LobbyMembershipResult.Failed(LobbyMembershipOutcome.Rejected, exception.Message);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return LobbyMembershipResult.Success();
            }
            catch (DbUpdateConcurrencyException) when (attempt < maximumAttempts - 1)
            {
                context.ChangeTracker.Clear();
            }
            catch (DbUpdateConcurrencyException)
            {
                return LobbyMembershipResult.Failed(
                    LobbyMembershipOutcome.ConcurrencyConflict,
                    "Lobby changed concurrently; retry the command with current state.");
            }
        }

        throw new InvalidOperationException("The bounded membership retry loop completed unexpectedly.");
    }
}
