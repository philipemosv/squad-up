using Microsoft.Extensions.DependencyInjection;
using SquadUp.Identity.Application;

namespace SquadUp.Identity.Infrastructure;

public static class SquadUpAuthorizationExtensions
{
    public const string ModeratorPolicy = "squad-up.moderator";
    public const string AdminPolicy = "squad-up.admin";

    public static IServiceCollection AddSquadUpAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthorization(options =>
        {
            options.AddPolicy(ModeratorPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(SquadUpRoles.Moderator, SquadUpRoles.Admin));
            options.AddPolicy(AdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(SquadUpRoles.Admin));
        });

        return services;
    }
}
