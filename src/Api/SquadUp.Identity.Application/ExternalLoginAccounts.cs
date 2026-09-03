namespace SquadUp.Identity.Application;

public interface IExternalLoginAccountService
{
    public Task<ExternalLoginUpsertResult> UpsertAsync(
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken);

    public Task<ExternalLoginLinkResult> LinkAsync(
        Guid userId,
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken);

    public Task<ExternalLoginUnlinkResult> UnlinkAsync(
        Guid userId,
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken);
}

public sealed record ExternalLoginUpsertResult(Guid UserId, bool WasCreated);

public enum ExternalLoginLinkResult
{
    Linked,
    AlreadyLinked,
    AccountNotFound,
    ExternalLoginCollision,
    ProviderAlreadyLinked
}

public enum ExternalLoginUnlinkResult
{
    Unlinked,
    NotLinked,
    AccountNotFound,
    WouldOrphanAccount
}
