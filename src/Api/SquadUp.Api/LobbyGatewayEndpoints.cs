using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Polly.CircuitBreaker;
using Polly.Timeout;
using SquadUp.Identity.Application;
using SquadUp.Identity.Infrastructure;

namespace SquadUp.Api;

/// <summary>
/// Public edge behavior for the small set of Lobby operations whose dependency
/// failure semantics are established here. The API owns browser authentication
/// and antiforgery; Lobby remains the owner of lobby state and authorization.
/// </summary>
internal static class LobbyGatewayEndpoints
{
    public static IEndpointRouteBuilder MapLobbyGateway(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var lobbies = endpoints.MapGroup("/lobbies").RequireAuthorization();
        lobbies.MapGet("", SearchAsync);
        lobbies.MapPost("/{lobbyId:guid}/cancel", CancelAsync);
        return endpoints;
    }

    private static Task<IResult> SearchAsync(
        HttpContext context,
        ILobbyClient lobbyClient) =>
        SendAsync(
            context,
            lobbyClient,
            new LobbyServiceRequest(
                HttpMethod.Get,
                QueryPath(context),
                RequirePlayerId(context),
                Roles(context),
                ["lobby.read"]));

    private static async Task<IResult> CancelAsync(
        HttpContext context,
        Guid lobbyId,
        ILobbyClient lobbyClient,
        IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return Problem(StatusCodes.Status400BadRequest, "antiforgery_validation_failed", "The antiforgery token is invalid.");
        }

        return await SendAsync(
            context,
            lobbyClient,
            new LobbyServiceRequest(
                HttpMethod.Post,
                $"/lobbies/{lobbyId:D}/cancel",
                RequirePlayerId(context),
                Roles(context),
                ["lobby.write"]));
    }

    internal static async Task<IResult> SendAsync(
        HttpContext context,
        ILobbyClient lobbyClient,
        LobbyServiceRequest request)
    {
        try
        {
            using var response = await lobbyClient.SendAsync(request, context.RequestAborted);
            if ((int)response.StatusCode >= StatusCodes.Status500InternalServerError)
            {
                return LobbyUnavailable();
            }

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return Results.NoContent();
            }

            // Lobby's HTTP boundary already emits minimized DTOs and sanitized Problem Details.
            // Preserve only its status and JSON body; do not forward dependency headers or credentials.
            var body = await response.Content.ReadAsStringAsync(context.RequestAborted);
            return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
        }
        catch (BrokenCircuitException)
        {
            return LobbyUnavailable();
        }
        catch (TimeoutRejectedException)
        {
            return LobbyUnavailable();
        }
        catch (HttpRequestException)
        {
            return LobbyUnavailable();
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            return LobbyUnavailable();
        }
    }

    private static string QueryPath(HttpContext context) =>
        $"/lobbies{context.Request.QueryString}";

    private static Guid RequirePlayerId(HttpContext context)
    {
        var subject = context.User.FindFirstValue(SquadUpClaimTypes.Subject);
        return Guid.TryParse(subject, out var playerId) && playerId != Guid.Empty
            ? playerId
            : throw new InvalidOperationException("Authenticated request is missing a valid subject claim.");
    }

    private static string[] Roles(HttpContext context) =>
        context.User.FindAll(SquadUpClaimTypes.Role).Select(claim => claim.Value).ToArray();

    private static IResult LobbyUnavailable() =>
        Problem(
            StatusCodes.Status503ServiceUnavailable,
            "lobby_temporarily_unavailable",
            "Lobbies are temporarily unavailable.");

    private static IResult Problem(int statusCode, string code, string title) =>
        Results.Problem(
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
