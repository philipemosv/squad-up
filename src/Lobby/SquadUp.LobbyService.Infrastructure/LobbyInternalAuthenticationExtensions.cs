using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace SquadUp.LobbyService.Infrastructure;

public static class LobbyInternalAuthenticationExtensions
{
    public const string AuthenticationScheme = "SquadUp.Internal";
    public const string ReadPolicy = "lobby.read";
    public const string WritePolicy = "lobby.write";
    public const string OwnerOrModeratorPolicy = "lobby.owner-or-moderator";

    public static IServiceCollection AddLobbyInternalAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<LobbyInternalAuthenticationOptions>()
            .Bind(configuration.GetSection(LobbyInternalAuthenticationOptions.SectionName))
            .Validate(LobbyInternalAuthenticationOptions.IsValid, LobbyInternalAuthenticationOptions.ValidationError)
            .ValidateOnStart();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AuthenticationScheme;
                options.DefaultChallengeScheme = AuthenticationScheme;
            })
            .AddJwtBearer(AuthenticationScheme, options =>
            {
                options.IncludeErrorDetails = false;
                options.MapInboundClaims = false;
            });

        services
            .AddOptions<JwtBearerOptions>(AuthenticationScheme)
            .Configure<IOptions<LobbyInternalAuthenticationOptions>>(ConfigureJwtBearer);

        services.AddAuthorization(options =>
        {
            options.AddPolicy(ReadPolicy, policy => RequireScope(policy, ReadPolicy));
            options.AddPolicy(WritePolicy, policy => RequireScope(policy, WritePolicy));
            options.AddPolicy(OwnerOrModeratorPolicy, policy =>
            {
                RequireScope(policy, WritePolicy);
                policy.AddRequirements(new LobbyOwnerOrModeratorRequirement());
            });
        });
        services.AddSingleton<IAuthorizationHandler, LobbyOwnerOrModeratorHandler>();

        return services;
    }

    private static void ConfigureJwtBearer(
        JwtBearerOptions bearer,
        IOptions<LobbyInternalAuthenticationOptions> configuredOptions)
    {
        var options = configuredOptions.Value;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            TryAllIssuerSigningKeys = false,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            IssuerSigningKeys = CreatePublicKeys(options.PublicKeys),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role"
        };
        bearer.Events = new JwtBearerEvents
        {
            OnTokenValidated = context => ValidateRequiredClaims(context, options),
            OnChallenge = context =>
            {
                context.HandleResponse();
                return WriteAuthenticationProblemAsync(context.HttpContext);
            },
            OnForbidden = context => WriteAuthorizationProblemAsync(context.HttpContext)
        };
    }

    private static IEnumerable<SecurityKey> CreatePublicKeys(
        IReadOnlyDictionary<string, string> configuredKeys)
    {
        foreach (var (keyId, publicKeyPem) in configuredKeys)
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            yield return new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: false))
            {
                KeyId = keyId
            };
        }
    }

    private static Task ValidateRequiredClaims(
        TokenValidatedContext context,
        LobbyInternalAuthenticationOptions options)
    {
        var principal = context.Principal;
        var subject = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var tokenId = principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var clientId = principal?.FindFirstValue("client_id");
        var tokenKind = principal?.FindFirstValue("token_kind");
        var scopes = GetScopes(principal);
        var issuedAtValue = principal?.FindFirstValue(JwtRegisteredClaimNames.Iat);
        var roles = principal?.FindAll("role").Select(claim => claim.Value).ToArray() ?? [];
        var hasBoundedLifetime = long.TryParse(issuedAtValue, out var issuedAtSeconds) &&
            TryGetUtcDateTime(issuedAtSeconds, out var issuedAt) &&
            context.SecurityToken.ValidTo > issuedAt &&
            context.SecurityToken.ValidTo - issuedAt <=
                TimeSpan.FromSeconds(options.MaximumTokenLifetimeSeconds);

        var validSubject = tokenKind switch
        {
            "workload" => string.Equals(subject, options.ApiClientId, StringComparison.Ordinal),
            "delegated_user" => Guid.TryParse(subject, out var userId) && userId != Guid.Empty,
            _ => false
        };
        var validRoles = roles.All(role => role is "Player" or "Moderator" or "Admin") &&
            (tokenKind == "delegated_user" || roles.Length == 0);
        if (!validSubject ||
            !Guid.TryParse(tokenId, out _) ||
            !string.Equals(clientId, options.ApiClientId, StringComparison.Ordinal) ||
            !hasBoundedLifetime ||
            scopes.Count == 0 ||
            scopes.Any(scope => !options.AllowedScopes.Contains(scope, StringComparer.Ordinal)) ||
            !validRoles)
        {
            context.Fail("The internal token is missing required bounded claims.");
        }

        return Task.CompletedTask;
    }

    private static Task WriteAuthenticationProblemAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Authentication is required.",
            extensions: new Dictionary<string, object?> { ["code"] = "authentication_required" })
            .ExecuteAsync(context);
    }

    private static Task WriteAuthorizationProblemAsync(HttpContext context) =>
        Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "The caller is not authorized for this lobby operation.",
            extensions: new Dictionary<string, object?> { ["code"] = "authorization_forbidden" })
            .ExecuteAsync(context);

    private static bool TryGetUtcDateTime(long unixTimeSeconds, out DateTime value)
    {
        try
        {
            value = DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds).UtcDateTime;
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            value = default;
            return false;
        }
    }

    private static void RequireScope(AuthorizationPolicyBuilder policy, string requiredScope)
    {
        policy.AddAuthenticationSchemes(AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => GetScopes(context.User).Contains(requiredScope));
    }

    private static HashSet<string> GetScopes(ClaimsPrincipal? principal) =>
        principal?
            .FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.Ordinal) ?? [];
}
