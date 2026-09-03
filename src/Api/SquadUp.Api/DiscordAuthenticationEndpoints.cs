using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using SquadUp.Identity.Application;
using SquadUp.Identity.Infrastructure;

namespace SquadUp.Api;

internal static class DiscordAuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapDiscordAuthentication(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/auth/discord/login", static async (HttpContext context) =>
        {
            await context.ChallengeAsync(
                DiscordOAuthDefaults.AuthenticationScheme,
                new AuthenticationProperties
                {
                    RedirectUri = DiscordOAuthDefaults.CompletionPath
                });
        });

        endpoints.MapGet(
            DiscordOAuthDefaults.CompletionPath,
            (Delegate)CompleteAuthenticationAsync);

        return endpoints;
    }

    private static async Task<IResult> CompleteAuthenticationAsync(
        HttpContext context,
        IExternalLoginAccountService externalLogins)
    {
        var authentication = await context.AuthenticateAsync(
            DiscordOAuthExtensions.ExternalCookieScheme);

        if (!authentication.Succeeded)
        {
            await context.SignOutAsync(DiscordOAuthExtensions.ExternalCookieScheme);
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Discord authentication could not be completed.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "discord_external_identity_missing"
                });
        }

        try
        {
            var discordUserId = authentication.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!DiscordOAuthDefaults.IsValidUserId(discordUserId))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Discord authentication could not be completed.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "discord_external_identity_invalid"
                    });
            }

            await externalLogins.UpsertAsync(
                DiscordOAuthDefaults.AuthenticationScheme,
                discordUserId!,
                context.RequestAborted);
            return Results.NoContent();
        }
        finally
        {
            await context.SignOutAsync(DiscordOAuthExtensions.ExternalCookieScheme);
        }
    }
}
