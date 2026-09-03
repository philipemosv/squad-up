using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SquadUp.Identity.Application;

namespace SquadUp.Identity.Infrastructure;

internal sealed class InternalAccessTokenIssuer : IInternalAccessTokenIssuer, IDisposable
{
    private const string WorkloadTokenKind = "workload";
    private const string DelegatedUserTokenKind = "delegated_user";
    private readonly InternalTokenOptions options;
    private readonly RSA rsa;
    private readonly SigningCredentials signingCredentials;
    private readonly TimeProvider timeProvider;

    public InternalAccessTokenIssuer(
        IOptions<InternalTokenOptions> options,
        TimeProvider timeProvider)
    {
        this.options = options.Value;
        this.timeProvider = timeProvider;
        rsa = RSA.Create();
        rsa.ImportFromPem(this.options.PrivateKeyPem);
        signingCredentials = new SigningCredentials(
            new RsaSecurityKey(rsa) { KeyId = this.options.ActiveKeyId },
            SecurityAlgorithms.RsaSha256);
    }

    public string Issue(InternalAccessTokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Scopes);

        if (!string.Equals(request.Audience, options.LobbyAudience, StringComparison.Ordinal))
        {
            throw new ArgumentException("The internal token audience is not allowed.", nameof(request));
        }

        if (!string.Equals(request.ClientId, options.ClientId, StringComparison.Ordinal))
        {
            throw new ArgumentException("The internal token client is not allowed.", nameof(request));
        }

        if (request.Scopes.Count is 0 or > 16)
        {
            throw new ArgumentException("An internal token requires a bounded scope set.", nameof(request));
        }

        var scopes = request.Scopes
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (scopes.Any(scope => !options.AllowedScopes.Contains(scope, StringComparer.Ordinal)))
        {
            throw new ArgumentException("Every internal token scope must be allowlisted.", nameof(request));
        }

        if (request.DelegatedUserId == Guid.Empty)
        {
            throw new ArgumentException("A delegated user ID cannot be empty.", nameof(request));
        }

        var roles = request.Roles?
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray() ?? [];
        if (roles.Any(role => !SquadUpRoles.IsDefined(role)) ||
            (request.DelegatedUserId is null && roles.Length > 0))
        {
            throw new ArgumentException(
                "Only delegated users may carry allowlisted application roles.",
                nameof(request));
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var isDelegated = request.DelegatedUserId is not null;
        var subject = isDelegated
            ? request.DelegatedUserId.GetValueOrDefault().ToString("D")
            : options.ClientId;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.LobbyAudience,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString("D")),
                new Claim("client_id", options.ClientId),
                new Claim("scope", string.Join(' ', scopes)),
                new Claim("token_kind", isDelegated ? DelegatedUserTokenKind : WorkloadTokenKind),
                .. roles.Select(role => new Claim(SquadUpClaimTypes.Role, role))
            ]),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddSeconds(options.LifetimeSeconds),
            SigningCredentials = signingCredentials
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public void Dispose() => rsa.Dispose();
}
