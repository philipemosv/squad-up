using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using SquadUp.Identity.Application;
using SquadUp.Profile.Application;

namespace SquadUp.Api;

internal static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfile(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var me = endpoints.MapGroup("/me").RequireAuthorization();
        me.MapGet("/profile", GetProfileAsync);
        me.MapPut("/profile", PutProfileAsync);
        me.MapGet("/games", ListGamesAsync);
        me.MapPut("/games/{gameId}", PutGameAsync);
        me.MapDelete("/games/{gameId}", DeleteGameAsync);

        var catalog = endpoints.MapGroup("/catalog");
        catalog.MapGet("/games", ListCatalogGamesAsync);
        catalog.MapGet("/games/{gameId}/ranks", ListCatalogRankTiersAsync);

        return endpoints;
    }

    private static async Task<IResult> GetProfileAsync(HttpContext context, IPlayerProfileService profiles)
    {
        var playerId = RequirePlayerId(context);
        var profile = await profiles.GetAsync(playerId, context.RequestAborted);
        return profile is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The profile does not exist yet.",
                extensions: Code("profile_not_found"))
            : Results.Ok(profile);
    }

    private static async Task<IResult> PutProfileAsync(
        HttpContext context,
        UpdateProfileRequest request,
        IPlayerProfileService profiles)
    {
        var playerId = RequirePlayerId(context);
        var result = await profiles.UpsertAsync(playerId, request, context.RequestAborted);

        return result.Outcome switch
        {
            ProfileMutationOutcome.Success => Results.Ok(result.Profile),
            ProfileMutationOutcome.ValidationFailed => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The profile request is invalid.",
                extensions: Code("profile_validation_failed", result.Error)),
            ProfileMutationOutcome.VersionRequired => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "An expected version is required to update an existing profile.",
                extensions: Code("profile_version_required")),
            ProfileMutationOutcome.VersionConflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The profile was modified by another request.",
                extensions: Code("profile_version_conflict")),
            _ => throw new InvalidOperationException($"Unhandled profile mutation outcome '{result.Outcome}'.")
        };
    }

    private static async Task<IResult> ListGamesAsync(HttpContext context, IPlayerGameService games)
    {
        var playerId = RequirePlayerId(context);
        var result = await games.ListAsync(playerId, context.RequestAborted);
        return Results.Ok(result);
    }

    private static async Task<IResult> PutGameAsync(
        HttpContext context,
        string gameId,
        PutPlayerGameRequest request,
        IPlayerGameService games)
    {
        var playerId = RequirePlayerId(context);
        var result = await games.UpsertAsync(playerId, gameId, request, context.RequestAborted);

        return result.Outcome switch
        {
            PlayerGameMutationOutcome.Success => Results.Ok(result.Game),
            PlayerGameMutationOutcome.GameNotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The requested game is not in the catalog.",
                extensions: Code("player_game_not_found", result.Error)),
            PlayerGameMutationOutcome.RankTierNotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The requested rank tier is not in the catalog.",
                extensions: Code("player_rank_tier_not_found", result.Error)),
            PlayerGameMutationOutcome.ValidationFailed => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The player game request is invalid.",
                extensions: Code("player_game_validation_failed", result.Error)),
            _ => throw new InvalidOperationException($"Unhandled player game mutation outcome '{result.Outcome}'.")
        };
    }

    private static async Task<IResult> DeleteGameAsync(HttpContext context, string gameId, IPlayerGameService games)
    {
        var playerId = RequirePlayerId(context);
        var outcome = await games.RemoveAsync(playerId, gameId, context.RequestAborted);
        return outcome switch
        {
            PlayerGameRemovalOutcome.Removed => Results.NoContent(),
            PlayerGameRemovalOutcome.NotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The player does not have that game.",
                extensions: Code("player_game_not_found")),
            _ => throw new InvalidOperationException($"Unhandled player game removal outcome '{outcome}'.")
        };
    }

    private static async Task<IResult> ListCatalogGamesAsync(
        IGameCatalogQueryService catalog,
        CancellationToken cancellationToken) =>
        Results.Ok(await catalog.ListGamesAsync(cancellationToken));

    private static async Task<IResult> ListCatalogRankTiersAsync(
        string gameId,
        IGameCatalogQueryService catalog,
        CancellationToken cancellationToken) =>
        Results.Ok(await catalog.ListRankTiersAsync(gameId, cancellationToken));

    private static Guid RequirePlayerId(HttpContext context)
    {
        var subject = context.User.FindFirstValue(SquadUpClaimTypes.Subject);
        return Guid.TryParse(subject, out var playerId) && playerId != Guid.Empty
            ? playerId
            : throw new InvalidOperationException("Authenticated request is missing a valid subject claim.");
    }

    private static Dictionary<string, object?> Code(string code, string? detail = null)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code };
        if (detail is not null)
        {
            extensions["detail"] = detail;
        }

        return extensions;
    }
}
