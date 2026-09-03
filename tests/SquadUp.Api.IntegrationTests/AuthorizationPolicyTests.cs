using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SquadUp.Identity.Application;
using SquadUp.Identity.Infrastructure;

namespace SquadUp.Api.IntegrationTests;

public sealed class AuthorizationPolicyTests
{
    [Fact]
    public async Task PlayerCannotEscalateToAdministrativePolicies()
    {
        await using var services = CreateServices();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var player = CreatePrincipal(SquadUpRoles.Player);

        var moderator = await authorization.AuthorizeAsync(
            player,
            resource: null,
            SquadUpAuthorizationExtensions.ModeratorPolicy);
        var admin = await authorization.AuthorizeAsync(
            player,
            resource: null,
            SquadUpAuthorizationExtensions.AdminPolicy);

        Assert.False(moderator.Succeeded);
        Assert.False(admin.Succeeded);
    }

    [Fact]
    public async Task AdministrativePoliciesAcceptOnlyTheirExplicitRoleHierarchy()
    {
        await using var services = CreateServices();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var moderator = CreatePrincipal(SquadUpRoles.Moderator);
        var admin = CreatePrincipal(SquadUpRoles.Admin);

        Assert.True((await authorization.AuthorizeAsync(
            moderator,
            resource: null,
            SquadUpAuthorizationExtensions.ModeratorPolicy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            moderator,
            resource: null,
            SquadUpAuthorizationExtensions.AdminPolicy)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(
            admin,
            resource: null,
            SquadUpAuthorizationExtensions.ModeratorPolicy)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(
            admin,
            resource: null,
            SquadUpAuthorizationExtensions.AdminPolicy)).Succeeded);
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSquadUpAuthorization();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static ClaimsPrincipal CreatePrincipal(string role) => new(new ClaimsIdentity(
        [
            new Claim(SquadUpClaimTypes.Subject, Guid.CreateVersion7().ToString("D")),
            new Claim(SquadUpClaimTypes.Role, role)
        ],
        "Test",
        SquadUpClaimTypes.Subject,
        SquadUpClaimTypes.Role));
}
