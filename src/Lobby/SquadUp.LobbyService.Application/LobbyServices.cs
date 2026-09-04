namespace SquadUp.LobbyService.Application;

public interface ILobbyCommandService
{
    public Task<CreateLobbyResult> CreateAsync(
        Guid ownerPlayerId,
        CreateLobbyRequest request,
        CancellationToken cancellationToken);
}

public interface ILobbyQueryService
{
    public Task<IReadOnlyList<LobbySummaryDto>> SearchRecruitingAsync(
        string? gameId,
        CancellationToken cancellationToken);
}
