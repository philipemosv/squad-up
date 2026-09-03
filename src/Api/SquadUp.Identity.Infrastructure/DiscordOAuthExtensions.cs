using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SquadUp.Identity.Infrastructure;

public static class DiscordOAuthExtensions
{
    public const string ExternalCookieScheme = "SquadUp.External";

    public static IServiceCollection AddDiscordOAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(DiscordOAuthDefaults.ConfigurationSection);

        services
            .AddOptions<DiscordOAuthOptions>()
            .Bind(section)
            .Validate(static options => IsValidClientId(options.ClientId),
                DiscordOAuthOptions.ClientIdValidationError)
            .Validate(static options => IsValidClientSecret(options.ClientSecret),
                DiscordOAuthOptions.ClientSecretValidationError)
            .ValidateOnStart();

        services.AddLogging(logging => logging.AddFilter(
            "Microsoft.AspNetCore.Authentication.OAuth",
            LogLevel.None));

        services
            .AddAuthentication()
            .AddCookie(ExternalCookieScheme, options =>
            {
                options.Cookie.Name = "__Host-SquadUp.External";
                options.Cookie.HttpOnly = true;
                options.Cookie.Path = "/";
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                options.SlidingExpiration = false;
            })
            .AddOAuth(DiscordOAuthDefaults.AuthenticationScheme, DiscordOAuthDefaults.DisplayName, options =>
            {
                options.SignInScheme = ExternalCookieScheme;
                options.AuthorizationEndpoint = DiscordOAuthDefaults.AuthorizationEndpoint;
                options.TokenEndpoint = DiscordOAuthDefaults.TokenEndpoint;
                options.UserInformationEndpoint = DiscordOAuthDefaults.UserInformationEndpoint;
                options.CallbackPath = DiscordOAuthDefaults.CallbackPath;
                options.SaveTokens = false;

                options.Scope.Clear();
                options.Scope.Add(DiscordOAuthDefaults.Scope);
                options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");

                options.CorrelationCookie.Name = "__Host-SquadUp.Correlation.";
                options.CorrelationCookie.HttpOnly = true;
                options.CorrelationCookie.Path = "/";
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.CorrelationCookie.MaxAge = TimeSpan.FromMinutes(5);

                options.Events = new OAuthEvents
                {
                    OnCreatingTicket = CreateTicketAsync,
                    OnRemoteFailure = HandleRemoteFailureAsync
                };
            });

        services
            .AddOptions<OAuthOptions>(DiscordOAuthDefaults.AuthenticationScheme)
            .Configure<IOptions<DiscordOAuthOptions>>(static (oauth, discord) =>
            {
                oauth.ClientId = discord.Value.ClientId;
                oauth.ClientSecret = discord.Value.ClientSecret;
            });

        return services;
    }

    private static async Task CreateTicketAsync(OAuthCreatingTicketContext context)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            DiscordOAuthDefaults.UserInformationEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

        using var response = await context.Backchannel.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            context.HttpContext.RequestAborted);
        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync(context.HttpContext.RequestAborted);
        using var user = await JsonDocument.ParseAsync(
            content,
            cancellationToken: context.HttpContext.RequestAborted);
        context.RunClaimActions(user.RootElement);
    }

    private static async Task HandleRemoteFailureAsync(RemoteFailureContext context)
    {
        context.HandleResponse();
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("SquadUp.Identity.DiscordOAuth");
        DiscordOAuthLog.CallbackFailed(logger);

        await Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Discord authentication could not be completed.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "discord_oauth_callback_invalid"
                })
            .ExecuteAsync(context.HttpContext);
    }

    private static bool IsValidClientId(string? clientId) =>
        clientId is not null &&
        clientId.Length is >= 17 and <= 32 &&
        clientId.All(char.IsAsciiDigit);

    private static bool IsValidClientSecret(string? clientSecret) =>
        clientSecret is not null && clientSecret.Length is >= 32 and <= 256;
}
