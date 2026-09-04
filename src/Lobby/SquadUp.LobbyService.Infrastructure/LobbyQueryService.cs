using Microsoft.EntityFrameworkCore;
using SquadUp.LobbyService.Application;
using SquadUp.LobbyService.Domain;

namespace SquadUp.LobbyService.Infrastructure;

internal sealed class LobbyQueryService(LobbyDbContext context) : ILobbyQueryService
{
    public async Task<LobbySearchPageDto> SearchRecruitingAsync(
        SearchRecruitingLobbiesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedGameId = request.GameId?.Trim().ToLowerInvariant();

        var summaries = await context.Lobbies
            .AsNoTracking()
            .Where(lobby => lobby.Status == LobbyStatus.Recruiting)
            .Where(lobby => normalizedGameId == null || lobby.RankRequirement.GameId == normalizedGameId)
            .Where(lobby => request.AfterLobbyId == null || lobby.Id.CompareTo(request.AfterLobbyId.Value) > 0)
            .OrderBy(lobby => lobby.Id)
            .Select(lobby => new LobbySummaryDto(
                lobby.Id,
                lobby.Capacity,
                lobby.MembersCount,
                lobby.RankRequirement.GameId,
                lobby.RankRequirement.MinimumOrdinal,
                lobby.RankRequirement.MaximumOrdinal))
            .Take(request.PageSize + 1)
            .ToListAsync(cancellationToken);

        var hasNextPage = summaries.Count > request.PageSize;
        var items = hasNextPage ? summaries.Take(request.PageSize).ToArray() : summaries.ToArray();
        return new LobbySearchPageDto(
            items,
            hasNextPage ? LobbySearchCursor.Encode(items[^1].LobbyId) : null);
    }
}
