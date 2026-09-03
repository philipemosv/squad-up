using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SquadUp.Identity.Application;

namespace SquadUp.Identity.Infrastructure;

internal sealed class ExternalLoginAccountService(IdentityDbContext context)
    : IExternalLoginAccountService
{
    public async Task<ExternalLoginUpsertResult> UpsertAsync(
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken)
    {
        ValidateLogin(loginProvider, providerKey);

        var existingUserId = await FindUserIdAsync(loginProvider, providerKey, cancellationToken);
        if (existingUserId is not null)
        {
            return new ExternalLoginUpsertResult(existingUserId.Value, WasCreated: false);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var user = new ApplicationUser();
        context.Users.Add(user);
        context.UserLogins.Add(CreateLogin(user.Id, loginProvider, providerKey));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ExternalLoginUpsertResult(user.Id, WasCreated: true);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();

            var winnerUserId = await FindUserIdAsync(loginProvider, providerKey, cancellationToken);
            if (winnerUserId is not null)
            {
                return new ExternalLoginUpsertResult(winnerUserId.Value, WasCreated: false);
            }

            throw;
        }
    }

    public async Task<ExternalLoginLinkResult> LinkAsync(
        Guid userId,
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken)
    {
        ValidateUserId(userId);
        ValidateLogin(loginProvider, providerKey);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockAndFindUserAsync(userId, cancellationToken);
        if (user is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ExternalLoginLinkResult.AccountNotFound;
        }

        var loginOwner = await FindUserIdAsync(loginProvider, providerKey, cancellationToken);
        if (loginOwner == userId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ExternalLoginLinkResult.AlreadyLinked;
        }

        if (loginOwner is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ExternalLoginLinkResult.ExternalLoginCollision;
        }

        var alreadyHasProvider = await context.UserLogins
            .AnyAsync(
                login => login.UserId == userId && login.LoginProvider == loginProvider,
                cancellationToken);
        if (alreadyHasProvider)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ExternalLoginLinkResult.ProviderAlreadyLinked;
        }

        context.UserLogins.Add(CreateLogin(userId, loginProvider, providerKey));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ExternalLoginLinkResult.Linked;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            return await ClassifyLinkConflictAsync(
                userId,
                loginProvider,
                providerKey,
                cancellationToken);
        }
    }

    public async Task<ExternalLoginUnlinkResult> UnlinkAsync(
        Guid userId,
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken)
    {
        ValidateUserId(userId);
        ValidateLogin(loginProvider, providerKey);

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var user = await LockAndFindUserAsync(userId, cancellationToken);
        if (user is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ExternalLoginUnlinkResult.AccountNotFound;
        }

        var login = await context.UserLogins.SingleOrDefaultAsync(
            candidate => candidate.UserId == userId &&
                candidate.LoginProvider == loginProvider &&
                candidate.ProviderKey == providerKey,
            cancellationToken);
        if (login is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ExternalLoginUnlinkResult.NotLinked;
        }

        var hasAlternativeLogin = await context.UserLogins.AnyAsync(
            candidate => candidate.UserId == userId &&
                (candidate.LoginProvider != loginProvider || candidate.ProviderKey != providerKey),
            cancellationToken);
        if (!hasAlternativeLogin && string.IsNullOrEmpty(user.PasswordHash))
        {
            await transaction.RollbackAsync(cancellationToken);
            return ExternalLoginUnlinkResult.WouldOrphanAccount;
        }

        context.UserLogins.Remove(login);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ExternalLoginUnlinkResult.Unlinked;
    }

    private async Task<ApplicationUser?> LockAndFindUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM identity.users WHERE id = {userId} FOR UPDATE",
            cancellationToken);
        return await context.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    private async Task<ExternalLoginLinkResult> ClassifyLinkConflictAsync(
        Guid userId,
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken)
    {
        var loginOwner = await FindUserIdAsync(loginProvider, providerKey, cancellationToken);
        if (loginOwner == userId)
        {
            return ExternalLoginLinkResult.AlreadyLinked;
        }

        if (loginOwner is not null)
        {
            return ExternalLoginLinkResult.ExternalLoginCollision;
        }

        return ExternalLoginLinkResult.ProviderAlreadyLinked;
    }

    private Task<Guid?> FindUserIdAsync(
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken) => context.UserLogins
        .AsNoTracking()
        .Where(login => login.LoginProvider == loginProvider && login.ProviderKey == providerKey)
        .Select(login => (Guid?)login.UserId)
        .SingleOrDefaultAsync(cancellationToken);

    private static IdentityUserLogin<Guid> CreateLogin(
        Guid userId,
        string loginProvider,
        string providerKey) => new()
        {
            UserId = userId,
            LoginProvider = loginProvider,
            ProviderKey = providerKey,
            ProviderDisplayName = loginProvider
        };

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A local user ID is required.", nameof(userId));
        }
    }

    private static void ValidateLogin(string loginProvider, string providerKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loginProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);

        if (loginProvider.Length > 128)
        {
            throw new ArgumentException("The login provider is too long.", nameof(loginProvider));
        }

        if (providerKey.Length > 256)
        {
            throw new ArgumentException("The provider key is too long.", nameof(providerKey));
        }
    }
}
