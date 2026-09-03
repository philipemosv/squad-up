using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
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

        endpoints.MapGet("/auth/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new AntiforgeryResponse(antiforgery.GetAndStoreTokens(context).RequestToken!));
        })
            .RequireAuthorization();

        endpoints.MapPost("/auth/logout", (Delegate)LogoutAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> CompleteAuthenticationAsync(
        HttpContext context,
        IExternalLoginAccountService externalLogins,
        IUserSessionClaimsProvider sessionClaimsProvider,
        ILogger<Program> logger)
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

            var localAccount = await externalLogins.UpsertAsync(
                DiscordOAuthDefaults.AuthenticationScheme,
                discordUserId!,
                context.RequestAborted);

            var sessionClaims = await sessionClaimsProvider.FindAsync(
                localAccount.UserId,
                context.RequestAborted);
            if (sessionClaims is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "The local account is not authorized.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "local_account_not_authorized"
                    });
            }

            var now = TimeProvider.System.GetUtcNow();
            var claims = new List<Claim>
            {
                new(SquadUpClaimTypes.Subject, sessionClaims.UserId.ToString("D")),
                new(ClaimTypes.AuthenticationMethod, DiscordOAuthDefaults.AuthenticationScheme)
            };
            claims.AddRange(sessionClaims.Roles.Select(role =>
                new Claim(SquadUpClaimTypes.Role, role)));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                claims,
                BrowserSessionExtensions.AuthenticationScheme,
                SquadUpClaimTypes.Subject,
                SquadUpClaimTypes.Role));
            await context.SignInAsync(
                BrowserSessionExtensions.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    AllowRefresh = false,
                    IsPersistent = false,
                    IssuedUtc = now,
                    ExpiresUtc = now.Add(BrowserSessionExtensions.SessionLifetime)
                });
            AuditIdentityAction(
                logger,
                context,
                "identity.discord.sign_in",
                localAccount.WasCreated ? "Created" : "Existing",
                localAccount.UserId);
            return Results.NoContent();
        }
        finally
        {
            await context.SignOutAsync(DiscordOAuthExtensions.ExternalCookieScheme);
        }
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        ILogger<Program> logger)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The antiforgery token is invalid.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "antiforgery_validation_failed"
                });
        }

        var playerId = RequirePlayerId(context);
        await context.SignOutAsync(BrowserSessionExtensions.AuthenticationScheme);
        AuditIdentityAction(logger, context, "identity.session.logout", "Success", playerId);
        return Results.NoContent();
    }

    private static Guid RequirePlayerId(HttpContext context)
    {
        var subject = context.User.FindFirstValue(SquadUpClaimTypes.Subject);
        return Guid.TryParse(subject, out var playerId) && playerId != Guid.Empty
            ? playerId
            : throw new InvalidOperationException("Authenticated request is missing a valid subject claim.");
    }

    private static void AuditIdentityAction(
        ILogger logger,
        HttpContext context,
        string action,
        string result,
        Guid playerId)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        IdentityAuditLog.ActionCompleted(
            logger,
            action,
            result,
            playerId,
            "identity.account",
            playerId,
            context.TraceIdentifier);
    }

    private sealed record AntiforgeryResponse(string RequestToken);
}
