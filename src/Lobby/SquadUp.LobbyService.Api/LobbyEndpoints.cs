using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using SquadUp.LobbyService.Application;
using SquadUp.LobbyService.Infrastructure;

namespace SquadUp.LobbyService.Api;

internal static class LobbyEndpoints
{
    public static IEndpointRouteBuilder MapLobby(this IEndpointRouteBuilder endpoints)
    {
        var lobbies = endpoints.MapGroup("/lobbies");
        lobbies.MapGet("", SearchAsync)
            .RequireAuthorization(LobbyInternalAuthenticationExtensions.ReadPolicy);
        lobbies.MapPost("", CreateAsync)
            .RequireAuthorization(LobbyInternalAuthenticationExtensions.WritePolicy);
        lobbies.MapPost("/{lobbyId:guid}/members", JoinAsync)
            .RequireAuthorization(LobbyInternalAuthenticationExtensions.WritePolicy);
        lobbies.MapDelete("/{lobbyId:guid}/members/me", LeaveAsync)
            .RequireAuthorization(LobbyInternalAuthenticationExtensions.WritePolicy);
        lobbies.MapPost("/{lobbyId:guid}/cancel", CancelAsync)
            .RequireAuthorization(LobbyInternalAuthenticationExtensions.WritePolicy);

        return endpoints;
    }

    private static async Task<IResult> SearchAsync(
        string? gameId,
        ILobbyQueryService lobbies,
        CancellationToken cancellationToken) =>
        Results.Ok(await lobbies.SearchRecruitingAsync(gameId, cancellationToken));

    private static async Task<IResult> CreateAsync(
        HttpContext context,
        CreateLobbyHttpRequest request,
        ILobbyCommandService lobbies)
    {
        var playerId = GetDelegatedPlayerId(context);
        if (playerId is null)
        {
            return DelegatedUserRequired();
        }

        return await ExecuteIdempotentlyAsync(
            context,
            playerId.Value,
            request,
            "POST /lobbies",
            async cancellationToken => CreateResponse(await lobbies.CreateAsync(
                playerId.Value,
                new CreateLobbyRequest(
                    request.Capacity,
                    request.GameId,
                    request.MinimumRankOrdinal,
                    request.MaximumRankOrdinal),
                cancellationToken)));
    }

    private static async Task<IResult> JoinAsync(
        HttpContext context,
        Guid lobbyId,
        JoinLobbyHttpRequest request,
        ILobbyCommandService lobbies)
    {
        var playerId = GetDelegatedPlayerId(context);
        if (playerId is null)
        {
            return DelegatedUserRequired();
        }

        return await ExecuteIdempotentlyAsync(
            context,
            playerId.Value,
            request,
            $"POST /lobbies/{lobbyId:D}/members",
            async cancellationToken => MembershipResponse(await lobbies.JoinAsync(
                lobbyId,
                playerId.Value,
                new JoinLobbyRequest(request.DiscordUserId, request.DisplayName, request.GameId, request.RankOrdinal),
                cancellationToken)));
    }

    private static async Task<IResult> LeaveAsync(
        HttpContext context,
        Guid lobbyId,
        ILobbyCommandService lobbies)
    {
        var playerId = GetDelegatedPlayerId(context);
        if (playerId is null)
        {
            return DelegatedUserRequired();
        }

        return MembershipResult(await lobbies.LeaveAsync(lobbyId, playerId.Value, context.RequestAborted));
    }

    private static async Task<IResult> CancelAsync(
        HttpContext context,
        Guid lobbyId,
        ILobbyCommandService lobbies)
    {
        if (GetDelegatedPlayerId(context) is null)
        {
            return DelegatedUserRequired();
        }

        var result = await lobbies.CancelAsync(lobbyId, context.User, context.RequestAborted);
        return result.Outcome switch
        {
            LobbyCancellationOutcome.Success => Results.NoContent(),
            LobbyCancellationOutcome.LobbyNotFound => Problem(StatusCodes.Status404NotFound, "lobby_not_found", result.Error),
            LobbyCancellationOutcome.Forbidden => Problem(StatusCodes.Status403Forbidden, "lobby_forbidden"),
            LobbyCancellationOutcome.ValidationFailed => Problem(StatusCodes.Status400BadRequest, "lobby_validation_failed", result.Error),
            LobbyCancellationOutcome.Rejected => Problem(StatusCodes.Status409Conflict, "lobby_rejected", result.Error),
            LobbyCancellationOutcome.ConcurrencyConflict => Problem(StatusCodes.Status409Conflict, "lobby_concurrency_conflict"),
            _ => throw new InvalidOperationException($"Unhandled lobby cancellation outcome '{result.Outcome}'.")
        };
    }

    private static IResult MembershipResult(LobbyMembershipResult result) => result.Outcome switch
    {
        LobbyMembershipOutcome.Success => Results.NoContent(),
        LobbyMembershipOutcome.LobbyNotFound => Problem(StatusCodes.Status404NotFound, "lobby_not_found", result.Error),
        LobbyMembershipOutcome.ValidationFailed => Problem(StatusCodes.Status400BadRequest, "lobby_validation_failed", result.Error),
        LobbyMembershipOutcome.Rejected => Problem(StatusCodes.Status409Conflict, "lobby_rejected", result.Error),
        LobbyMembershipOutcome.ConcurrencyConflict => Problem(StatusCodes.Status409Conflict, "lobby_concurrency_conflict"),
        _ => throw new InvalidOperationException($"Unhandled lobby membership outcome '{result.Outcome}'.")
    };

    private static HttpIdempotencyResponse CreateResponse(CreateLobbyResult result) => result.Outcome switch
    {
        CreateLobbyOutcome.Success => new(
            StatusCodes.Status201Created,
            Location: $"/lobbies/{result.LobbyId}",
            Body: JsonSerializer.Serialize(new { lobbyId = result.LobbyId })),
        CreateLobbyOutcome.GameNotFound => Failure(StatusCodes.Status404NotFound, "lobby_game_not_found", result.Error),
        CreateLobbyOutcome.RankTierNotFound => Failure(StatusCodes.Status404NotFound, "lobby_rank_tier_not_found", result.Error),
        CreateLobbyOutcome.ValidationFailed => Failure(StatusCodes.Status400BadRequest, "lobby_validation_failed", result.Error),
        _ => throw new InvalidOperationException($"Unhandled lobby creation outcome '{result.Outcome}'.")
    };

    private static HttpIdempotencyResponse MembershipResponse(LobbyMembershipResult result) => result.Outcome switch
    {
        LobbyMembershipOutcome.Success => new(StatusCodes.Status204NoContent),
        LobbyMembershipOutcome.LobbyNotFound => Failure(StatusCodes.Status404NotFound, "lobby_not_found", result.Error),
        LobbyMembershipOutcome.ValidationFailed => Failure(StatusCodes.Status400BadRequest, "lobby_validation_failed", result.Error),
        LobbyMembershipOutcome.Rejected => Failure(StatusCodes.Status409Conflict, "lobby_rejected", result.Error),
        LobbyMembershipOutcome.ConcurrencyConflict => Failure(StatusCodes.Status409Conflict, "lobby_concurrency_conflict", result.Error),
        _ => throw new InvalidOperationException($"Unhandled lobby membership outcome '{result.Outcome}'.")
    };

    private static async Task<IResult> ExecuteIdempotentlyAsync<TRequest>(
        HttpContext context,
        Guid playerId,
        TRequest request,
        string operation,
        Func<CancellationToken, Task<HttpIdempotencyResponse>> execute)
    {
        if (!TryGetIdempotencyKey(context, out var key, out var failure))
        {
            return failure;
        }

        var requestHash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { operation, request }));
        try
        {
            var response = await context.RequestServices.GetRequiredService<IHttpIdempotencyLedger>().ExecuteAsync(
                playerId,
                key,
                requestHash,
                execute,
                context.RequestAborted);
            return ToResult(response);
        }
        catch (HttpIdempotencyConflictException)
        {
            return Problem(StatusCodes.Status409Conflict, "idempotency_key_conflict");
        }
    }

    private static bool TryGetIdempotencyKey(HttpContext context, out string key, out IResult failure)
    {
        if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var values) || values.Count != 1)
        {
            key = string.Empty;
            failure = Problem(StatusCodes.Status400BadRequest, "idempotency_key_required");
            return false;
        }

        key = values[0] ?? string.Empty;
        if (key.Length is 0 or > IHttpIdempotencyLedger.MaximumKeyLength || key.Any(character => character is < '!' or > '~'))
        {
            failure = Problem(StatusCodes.Status400BadRequest, "idempotency_key_invalid");
            return false;
        }

        failure = null!;
        return true;
    }

    private static HttpIdempotencyResponse Failure(int statusCode, string code, string? detail = null) =>
        new(statusCode, code, detail);

    private static IResult ToResult(HttpIdempotencyResponse response)
    {
        if (response.Code is not null)
        {
            return Problem(response.StatusCode, response.Code, response.Detail);
        }

        if (response.Body is not null)
        {
            using var document = JsonDocument.Parse(response.Body);
            var body = document.RootElement.Clone();
            return response.Location is null
                ? Results.Json(body, statusCode: response.StatusCode)
                : Results.Created(response.Location, body);
        }

        return Results.StatusCode(response.StatusCode);
    }

    private static Guid? GetDelegatedPlayerId(HttpContext context)
    {
        if (!string.Equals(
                context.User.FindFirstValue("token_kind"),
                "delegated_user",
                StringComparison.Ordinal))
        {
            return null;
        }

        var subject = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(subject, out var playerId) && playerId != Guid.Empty ? playerId : null;
    }

    private static IResult DelegatedUserRequired() =>
        Problem(StatusCodes.Status403Forbidden, "delegated_user_required");

    private static IResult Problem(int statusCode, string code, string? detail = null) =>
        Results.Problem(
            statusCode: statusCode,
            title: "The lobby request could not be completed.",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}

internal sealed record CreateLobbyHttpRequest(
    int Capacity,
    string GameId,
    int MinimumRankOrdinal,
    int? MaximumRankOrdinal);

internal sealed record JoinLobbyHttpRequest(
    string DiscordUserId,
    string DisplayName,
    string GameId,
    int RankOrdinal);
