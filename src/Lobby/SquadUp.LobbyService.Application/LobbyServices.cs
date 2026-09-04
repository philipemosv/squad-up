namespace SquadUp.LobbyService.Application;

public interface ILobbyCommandService
{
    public Task<CreateLobbyResult> CreateAsync(
        Guid ownerPlayerId,
        CreateLobbyRequest request,
        CancellationToken cancellationToken);

    public Task<LobbyMembershipResult> JoinAsync(
        Guid lobbyId,
        Guid playerId,
        JoinLobbyRequest request,
        CancellationToken cancellationToken);

    public Task<LobbyMembershipResult> LeaveAsync(
        Guid lobbyId,
        Guid playerId,
        CancellationToken cancellationToken);
}

public interface ILobbyQueryService
{
    public Task<IReadOnlyList<LobbySummaryDto>> SearchRecruitingAsync(
        string? gameId,
        CancellationToken cancellationToken);
}
