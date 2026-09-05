using System.Security.Claims;

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

    public Task<LobbyCancellationResult> CancelAsync(
        Guid lobbyId,
        ClaimsPrincipal actor,
        CancellationToken cancellationToken);
}

public interface ILobbyQueryService
{
    public Task<LobbySearchPageDto> SearchRecruitingAsync(
        SearchRecruitingLobbiesRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Marks a minimized projection that is explicitly suitable for the Lobby read cache.
/// </summary>
public interface IAllowlistedLobbyReadProjection
{
}

/// <summary>
/// Provides cache-aside reads for allowlisted projections. Callers must supply
/// server-generated keys; key construction and expiration policy are introduced separately.
/// </summary>
public interface ILobbyReadCache
{
    public Task<TProjection> GetOrCreateAsync<TProjection>(
        string cacheKey,
        Func<CancellationToken, ValueTask<TProjection>> factory,
        CancellationToken cancellationToken)
        where TProjection : class, IAllowlistedLobbyReadProjection;
}
