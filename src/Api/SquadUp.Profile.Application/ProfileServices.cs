namespace SquadUp.Profile.Application;

public interface IPlayerProfileService
{
    public Task<ProfileDto?> GetAsync(Guid playerId, CancellationToken cancellationToken);

    public Task<ProfileMutationResult> UpsertAsync(
        Guid playerId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken);
}

public interface IPlayerGameService
{
    public Task<IReadOnlyList<PlayerGameDto>> ListAsync(Guid playerId, CancellationToken cancellationToken);

    public Task<PlayerGameMutationResult> UpsertAsync(
        Guid playerId,
        string gameId,
        PutPlayerGameRequest request,
        CancellationToken cancellationToken);

    public Task<PlayerGameRemovalOutcome> RemoveAsync(
        Guid playerId,
        string gameId,
        CancellationToken cancellationToken);
}

public interface IGameCatalogQueryService
{
    public Task<IReadOnlyList<GameCatalogDto>> ListGamesAsync(CancellationToken cancellationToken);

    public Task<IReadOnlyList<RankTierCatalogDto>> ListRankTiersAsync(
        string gameId,
        CancellationToken cancellationToken);
}
