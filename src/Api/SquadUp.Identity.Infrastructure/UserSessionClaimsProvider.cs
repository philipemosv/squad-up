using Microsoft.EntityFrameworkCore;
using SquadUp.Identity.Application;

namespace SquadUp.Identity.Infrastructure;

internal sealed class UserSessionClaimsProvider(IdentityDbContext context)
    : IUserSessionClaimsProvider
{
    public async Task<UserSessionClaims?> FindAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        var userExists = await context.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId, cancellationToken);
        if (!userExists)
        {
            return null;
        }

        var storedRoles = await (
            from userRole in context.UserRoles.AsNoTracking()
            join role in context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == userId && role.Name != null
            select role.Name)
            .ToArrayAsync(cancellationToken);
        var roles = storedRoles
            .Where(SquadUpRoles.IsDefined)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new UserSessionClaims(userId, roles);
    }
}
