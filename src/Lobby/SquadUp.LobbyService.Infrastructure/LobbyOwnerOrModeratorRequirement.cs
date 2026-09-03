using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using SquadUp.LobbyService.Application;

namespace SquadUp.LobbyService.Infrastructure;

public sealed class LobbyOwnerOrModeratorRequirement : IAuthorizationRequirement;

internal sealed class LobbyOwnerOrModeratorHandler
    : AuthorizationHandler<LobbyOwnerOrModeratorRequirement, LobbyAuthorizationResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        LobbyOwnerOrModeratorRequirement requirement,
        LobbyAuthorizationResource resource)
    {
        var subject = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var isOwner = Guid.TryParse(subject, out var userId) &&
            userId != Guid.Empty &&
            userId == resource.OwnerUserId;
        var isModerator = context.User.IsInRole("Moderator") || context.User.IsInRole("Admin");

        if (isOwner || isModerator)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
