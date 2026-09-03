using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SquadUp.Identity.Application;
using SquadUp.Identity.Infrastructure;
using SquadUp.LobbyService.Application;
using SquadUp.LobbyService.Infrastructure;

namespace SquadUp.Api.IntegrationTests;

public sealed class InternalTokenTests
{
    private const string Issuer = "https://api.squad-up.test";
    private const string Audience = "squad-up-lobby";
    private const string ClientId = "squad-up-api";
    private const string ActiveKeyId = "current-2026-09";
    private const string PreviousKeyId = "previous-2026-08";
    private readonly KeyMaterial activeKey = KeyMaterial.Create(ActiveKeyId);
    private readonly KeyMaterial previousKey = KeyMaterial.Create(PreviousKeyId);

    [Fact]
    public async Task IssuerCreatesAsymmetricShortAudienceBoundTokensWithExplicitActorKind()
    {
        using var host = await CreateIssuerHostAsync();
        var issuer = host.Services.GetRequiredService<IInternalAccessTokenIssuer>();
        var delegatedUserId = Guid.CreateVersion7();

        var workloadToken = issuer.Issue(new InternalAccessTokenRequest(
            Audience,
            ClientId,
            [LobbyInternalAuthenticationExtensions.ReadPolicy]));
        var delegatedToken = issuer.Issue(new InternalAccessTokenRequest(
            Audience,
            ClientId,
            [LobbyInternalAuthenticationExtensions.WritePolicy],
            delegatedUserId,
            [SquadUpRoles.Player]));

        AssertToken(
            workloadToken,
            ClientId,
            "workload",
            LobbyInternalAuthenticationExtensions.ReadPolicy);
        AssertToken(
            delegatedToken,
            delegatedUserId.ToString("D"),
            "delegated_user",
            LobbyInternalAuthenticationExtensions.WritePolicy,
            SquadUpRoles.Player);
    }

    [Fact]
    public async Task IssuerRefusesUnknownAudienceClientAndScope()
    {
        using var host = await CreateIssuerHostAsync();
        var issuer = host.Services.GetRequiredService<IInternalAccessTokenIssuer>();

        Assert.Throws<ArgumentException>(() => issuer.Issue(new InternalAccessTokenRequest(
            "squad-up-other-service",
            ClientId,
            [LobbyInternalAuthenticationExtensions.ReadPolicy])));
        Assert.Throws<ArgumentException>(() => issuer.Issue(new InternalAccessTokenRequest(
            Audience,
            "unknown-client",
            [LobbyInternalAuthenticationExtensions.ReadPolicy])));
        Assert.Throws<ArgumentException>(() => issuer.Issue(new InternalAccessTokenRequest(
            Audience,
            ClientId,
            ["profile.write"])));
        Assert.Throws<ArgumentException>(() => issuer.Issue(new InternalAccessTokenRequest(
            Audience,
            ClientId,
            [LobbyInternalAuthenticationExtensions.WritePolicy],
            Roles: [SquadUpRoles.Admin])));
        Assert.Throws<ArgumentException>(() => issuer.Issue(new InternalAccessTokenRequest(
            Audience,
            ClientId,
            [LobbyInternalAuthenticationExtensions.WritePolicy],
            Guid.CreateVersion7(),
            ["SuperAdmin"])));
    }

    [Fact]
    public async Task ResourcePolicyAllowsOwnerOrModeratorAndRejectsDifferentPlayer()
    {
        using var issuerHost = await CreateIssuerHostAsync();
        var issuer = issuerHost.Services.GetRequiredService<IInternalAccessTokenIssuer>();
        await using var lobby = await CreateLobbyApplicationAsync();
        using var client = lobby.GetTestClient();
        var ownerId = Guid.CreateVersion7();
        var differentUserId = Guid.CreateVersion7();
        var ownerToken = IssueDelegatedToken(issuer, ownerId, SquadUpRoles.Player);
        var differentPlayerToken = IssueDelegatedToken(issuer, differentUserId, SquadUpRoles.Player);
        var moderatorToken = IssueDelegatedToken(issuer, differentUserId, SquadUpRoles.Moderator);

        using var owner = await SendBearerAsync(client, $"/owned/{ownerId:D}", ownerToken);
        using var differentPlayer = await SendBearerAsync(
            client,
            $"/owned/{ownerId:D}",
            differentPlayerToken);
        using var moderator = await SendBearerAsync(client, $"/owned/{ownerId:D}", moderatorToken);

        Assert.Equal(HttpStatusCode.NoContent, owner.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, differentPlayer.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, moderator.StatusCode);
    }

    [Fact]
    public async Task LobbyAcceptsCurrentAndPreviousPublicKeysAndEnforcesScope()
    {
        using var issuerHost = await CreateIssuerHostAsync();
        var issuer = issuerHost.Services.GetRequiredService<IInternalAccessTokenIssuer>();
        await using var lobby = await CreateLobbyApplicationAsync();
        using var client = lobby.GetTestClient();
        var currentToken = issuer.Issue(new InternalAccessTokenRequest(
            Audience,
            ClientId,
            [LobbyInternalAuthenticationExtensions.ReadPolicy]));
        var previousToken = CreateToken(previousKey);

        using var currentRead = await SendBearerAsync(client, "/read", currentToken);
        using var previousRead = await SendBearerAsync(client, "/read", previousToken);
        using var wrongScope = await SendBearerAsync(client, "/write", currentToken);

        Assert.Equal(HttpStatusCode.NoContent, currentRead.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, previousRead.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongScope.StatusCode);
    }

    [Theory]
    [InlineData(InvalidTokenKind.WrongSignature)]
    [InlineData(InvalidTokenKind.WrongAlgorithm)]
    [InlineData(InvalidTokenKind.UnknownKeyId)]
    [InlineData(InvalidTokenKind.WrongIssuer)]
    [InlineData(InvalidTokenKind.WrongAudience)]
    [InlineData(InvalidTokenKind.Expired)]
    [InlineData(InvalidTokenKind.OverlongLifetime)]
    [InlineData(InvalidTokenKind.UnallowedScope)]
    public async Task LobbyRejectsTokensOutsideTheCryptographicAndClaimBoundary(
        InvalidTokenKind invalidTokenKind)
    {
        await using var lobby = await CreateLobbyApplicationAsync();
        using var client = lobby.GetTestClient();
        var token = CreateInvalidToken(invalidTokenKind);

        using var response = await SendBearerAsync(client, "/read", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(token, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidSigningConfigurationStopsStartupWithoutEchoingKeyMaterial()
    {
        const string invalidPrivateKey = "synthetic-invalid-private-key";
        var configuration = CreateIssuerConfiguration(invalidPrivateKey);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddInternalTokenIssuer(configuration);
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("InternalTokens", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidPrivateKey, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LobbyRejectsPrivateSigningKeyConfiguration()
    {
        var configuration = CreateLobbyConfiguration(activeKey.PrivateKeyPem);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddLobbyInternalAuthentication(configuration);
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("InternalAuthentication", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(activeKey.PrivateKeyPem, exception.Message, StringComparison.Ordinal);
    }

    private async Task<IHost> CreateIssuerHostAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddInternalTokenIssuer(CreateIssuerConfiguration(activeKey.PrivateKeyPem));
        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private async Task<WebApplication> CreateLobbyApplicationAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddLobbyInternalAuthentication(CreateLobbyConfiguration(activeKey.PublicKeyPem));
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/read", static () => Results.NoContent())
            .RequireAuthorization(LobbyInternalAuthenticationExtensions.ReadPolicy);
        app.MapGet("/write", static () => Results.NoContent())
            .RequireAuthorization(LobbyInternalAuthenticationExtensions.WritePolicy);
        app.MapGet("/owned/{ownerUserId:guid}", AuthorizeOwnedLobbyAsync)
            .RequireAuthorization(LobbyInternalAuthenticationExtensions.WritePolicy);
        await app.StartAsync();
        return app;
    }

    private static async Task<IResult> AuthorizeOwnedLobbyAsync(
        Guid ownerUserId,
        HttpContext context,
        IAuthorizationService authorization)
    {
        var result = await authorization.AuthorizeAsync(
            context.User,
            new LobbyAuthorizationResource(ownerUserId),
            LobbyInternalAuthenticationExtensions.OwnerOrModeratorPolicy);
        return result.Succeeded ? Results.NoContent() : Results.Forbid();
    }

    private static string IssueDelegatedToken(
        IInternalAccessTokenIssuer issuer,
        Guid userId,
        string role) => issuer.Issue(new InternalAccessTokenRequest(
            Audience,
            ClientId,
            [LobbyInternalAuthenticationExtensions.WritePolicy],
            userId,
            [role]));

    private static IConfiguration CreateIssuerConfiguration(string privateKeyPem) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalTokens:Issuer"] = Issuer,
                ["InternalTokens:LobbyAudience"] = Audience,
                ["InternalTokens:ClientId"] = ClientId,
                ["InternalTokens:LifetimeSeconds"] = "120",
                ["InternalTokens:ActiveKeyId"] = ActiveKeyId,
                ["InternalTokens:PrivateKeyPem"] = privateKeyPem,
                ["InternalTokens:AllowedScopes:0"] = LobbyInternalAuthenticationExtensions.ReadPolicy,
                ["InternalTokens:AllowedScopes:1"] = LobbyInternalAuthenticationExtensions.WritePolicy
            })
            .Build();

    private IConfiguration CreateLobbyConfiguration(string activePublicKeyPem) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalAuthentication:Issuer"] = Issuer,
                ["InternalAuthentication:Audience"] = Audience,
                ["InternalAuthentication:ApiClientId"] = ClientId,
                ["InternalAuthentication:MaximumTokenLifetimeSeconds"] = "120",
                ["InternalAuthentication:AllowedScopes:0"] = LobbyInternalAuthenticationExtensions.ReadPolicy,
                ["InternalAuthentication:AllowedScopes:1"] = LobbyInternalAuthenticationExtensions.WritePolicy,
                [$"InternalAuthentication:PublicKeys:{ActiveKeyId}"] = activePublicKeyPem,
                [$"InternalAuthentication:PublicKeys:{PreviousKeyId}"] = previousKey.PublicKeyPem
            })
            .Build();

    private static async Task<HttpResponseMessage> SendBearerAsync(
        HttpClient client,
        string path,
        string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static void AssertToken(
        string encodedToken,
        string subject,
        string tokenKind,
        string scope,
        string? role = null)
    {
        var token = new JsonWebTokenHandler().ReadJsonWebToken(encodedToken);

        Assert.Equal(SecurityAlgorithms.RsaSha256, token.Alg);
        Assert.Equal(ActiveKeyId, token.Kid);
        Assert.Equal(Issuer, token.Issuer);
        Assert.Equal(Audience, Assert.Single(token.Audiences));
        Assert.Equal(subject, token.Subject);
        Assert.Equal(ClientId, token.GetClaim("client_id").Value);
        Assert.Equal(tokenKind, token.GetClaim("token_kind").Value);
        Assert.Equal(scope, token.GetClaim("scope").Value);
        if (role is not null)
        {
            Assert.Equal(role, token.GetClaim(SquadUpClaimTypes.Role).Value);
        }
        Assert.True(Guid.TryParse(token.Id, out _));
        Assert.InRange(token.ValidTo - token.IssuedAt, TimeSpan.FromSeconds(119), TimeSpan.FromSeconds(121));
    }

    private string CreateInvalidToken(InvalidTokenKind kind)
    {
        using var attackerRsa = RSA.Create(2048);
        var signingKey = kind == InvalidTokenKind.WrongSignature
            ? new RsaSecurityKey(attackerRsa) { KeyId = ActiveKeyId }
            : activeKey.CreateSecurityKey();
        if (kind == InvalidTokenKind.UnknownKeyId)
        {
            signingKey.KeyId = "unknown-key";
        }
        var now = DateTime.UtcNow;
        return CreateToken(
            signingKey,
            kind == InvalidTokenKind.WrongAlgorithm ? SecurityAlgorithms.RsaSha512 : SecurityAlgorithms.RsaSha256,
            kind == InvalidTokenKind.WrongIssuer ? "https://attacker.invalid" : Issuer,
            kind == InvalidTokenKind.WrongAudience ? "squad-up-other-service" : Audience,
            kind == InvalidTokenKind.Expired ? now.AddMinutes(-3) : now,
            kind switch
            {
                InvalidTokenKind.Expired => now.AddMinutes(-2),
                InvalidTokenKind.OverlongLifetime => now.AddMinutes(3),
                _ => now.AddMinutes(2)
            },
            kind == InvalidTokenKind.UnallowedScope ? "profile.write" : LobbyInternalAuthenticationExtensions.ReadPolicy);
    }

    private static string CreateToken(KeyMaterial key) => CreateToken(
        key.CreateSecurityKey(),
        SecurityAlgorithms.RsaSha256,
        Issuer,
        Audience,
        DateTime.UtcNow,
        DateTime.UtcNow.AddMinutes(2),
        LobbyInternalAuthenticationExtensions.ReadPolicy);

    private static string CreateToken(
        SecurityKey signingKey,
        string algorithm,
        string issuer,
        string audience,
        DateTime notBefore,
        DateTime expires,
        string scope)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, ClientId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString("D")),
                new Claim("client_id", ClientId),
                new Claim("scope", scope),
                new Claim("token_kind", "workload")
            ]),
            IssuedAt = notBefore,
            NotBefore = notBefore,
            Expires = expires,
            SigningCredentials = new SigningCredentials(signingKey, algorithm)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public enum InvalidTokenKind
    {
        WrongSignature,
        WrongAlgorithm,
        UnknownKeyId,
        WrongIssuer,
        WrongAudience,
        Expired,
        OverlongLifetime,
        UnallowedScope
    }

    private sealed record KeyMaterial(string KeyId, string PrivateKeyPem, string PublicKeyPem)
    {
        public static KeyMaterial Create(string keyId)
        {
            using var rsa = RSA.Create(2048);
            return new KeyMaterial(
                keyId,
                rsa.ExportRSAPrivateKeyPem(),
                rsa.ExportSubjectPublicKeyInfoPem());
        }

        public RsaSecurityKey CreateSecurityKey()
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(PrivateKeyPem);
            return new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: true))
            {
                KeyId = KeyId
            };
        }
    }
}
