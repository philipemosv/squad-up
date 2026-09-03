using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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
        });

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
            NameClaimType = JwtRegisteredClaimNames.Sub
        };
        bearer.Events = new JwtBearerEvents
        {
            OnTokenValidated = context => ValidateRequiredClaims(context, options)
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
        if (!validSubject ||
            !Guid.TryParse(tokenId, out _) ||
            !string.Equals(clientId, options.ApiClientId, StringComparison.Ordinal) ||
            !hasBoundedLifetime ||
            scopes.Count == 0 ||
            scopes.Any(scope => !options.AllowedScopes.Contains(scope, StringComparer.Ordinal)))
        {
            context.Fail("The internal token is missing required bounded claims.");
        }

        return Task.CompletedTask;
    }

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
