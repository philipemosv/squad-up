using Microsoft.EntityFrameworkCore;
using SquadUp.LobbyService.Application;
using SquadUp.LobbyService.Domain;

namespace SquadUp.LobbyService.Infrastructure;

internal sealed class LobbyQueryService(LobbyDbContext context) : ILobbyQueryService
{
    public async Task<IReadOnlyList<LobbySummaryDto>> SearchRecruitingAsync(
        string? gameId,
        CancellationToken cancellationToken)
    {
        var normalizedGameId = gameId?.Trim().ToLowerInvariant();

        return await context.Lobbies
            .AsNoTracking()
            .Where(lobby => lobby.Status == LobbyStatus.Recruiting)
            .Where(lobby => normalizedGameId == null || lobby.RankRequirement.GameId == normalizedGameId)
            .OrderBy(lobby => lobby.Id)
            .Select(lobby => new LobbySummaryDto(
                lobby.Id,
                lobby.Capacity,
                lobby.MembersCount,
                lobby.RankRequirement.GameId,
                lobby.RankRequirement.MinimumOrdinal,
                lobby.RankRequirement.MaximumOrdinal))
            .ToListAsync(cancellationToken);
    }
}
