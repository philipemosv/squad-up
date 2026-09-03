using Microsoft.AspNetCore.Authentication;
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

    private static async Task<IResult> CompleteAuthenticationAsync(HttpContext context)
    {
        var authentication = await context.AuthenticateAsync(
            DiscordOAuthExtensions.ExternalCookieScheme);
        await context.SignOutAsync(DiscordOAuthExtensions.ExternalCookieScheme);

        if (!authentication.Succeeded)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Discord authentication could not be completed.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "discord_external_identity_missing"
                });
        }

        return Results.NoContent();
    }
}
