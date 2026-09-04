using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SquadUp.LobbyService.Infrastructure;

namespace SquadUp.LobbyService.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class LobbyEndpointTests : IClassFixture<LobbyDatabaseFixture>
{
    private const string Issuer = "https://api.squad-up.test";
    private const string Audience = "squad-up-lobby";
    private const string ClientId = "squad-up-api";
    private const string KeyId = "test-current";
    private readonly LobbyDatabaseFixture fixture;

    public LobbyEndpointTests(LobbyDatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task EndpointsBindOnlyExplicitInputsAndEnforceCurrentResourceAuthorization()
    {
        await using var application = new LobbyApplication(fixture.PostgreSql.GetConnectionString());
        await application.MigrateAsync();
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        var ownerId = Guid.CreateVersion7();
        var otherPlayerId = Guid.CreateVersion7();
        var ownerToken = application.CreateDelegatedToken(ownerId, "lobby.read lobby.write");
        var otherToken = application.CreateDelegatedToken(otherPlayerId, "lobby.read lobby.write");

        using var anonymousCreate = await SendJsonAsync(client, HttpMethod.Post, "/lobbies", null, new
        {
            capacity = 2,
            gameId = "dota2",
            minimumRankOrdinal = 1
        });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousCreate.StatusCode);
        await AssertProblemCodeAsync(anonymousCreate, "authentication_required");

        using var create = await SendJsonAsync(client, HttpMethod.Post, "/lobbies", ownerToken, new
        {
            capacity = 2,
            gameId = "dota2",
            minimumRankOrdinal = 1,
            ownerPlayerId = otherPlayerId
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var lobbyId = created.RootElement.GetProperty("lobbyId").GetGuid();

        using var search = await SendAsync(client, HttpMethod.Get, "/lobbies?gameId=dota2", ownerToken);
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        Assert.Contains(lobbyId.ToString("D"), await search.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        using var forbiddenCancel = await SendAsync(client, HttpMethod.Post, $"/lobbies/{lobbyId}/cancel", otherToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCancel.StatusCode);
        await AssertProblemCodeAsync(forbiddenCancel, "lobby_forbidden");

        using var join = await SendJsonAsync(client, HttpMethod.Post, $"/lobbies/{lobbyId}/members", otherToken, new
        {
            discordUserId = "synthetic-discord-id",
            displayName = "Synthetic Player",
            gameId = "dota2",
            rankOrdinal = 4,
            playerId = ownerId
        });
        Assert.Equal(HttpStatusCode.NoContent, join.StatusCode);
        await AssertMemberOwnedByAuthenticatedSubjectAsync(application, lobbyId, otherPlayerId);

        using var leave = await SendAsync(client, HttpMethod.Delete, $"/lobbies/{lobbyId}/members/me", otherToken);
        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);
        using var cancel = await SendAsync(client, HttpMethod.Post, $"/lobbies/{lobbyId}/cancel", ownerToken);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
    }

    [Fact]
    public async Task WorkloadTokensCannotActAsAPlayerAndFailuresUseProblemDetails()
    {
        await using var application = new LobbyApplication(fixture.PostgreSql.GetConnectionString());
        await application.MigrateAsync();
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var workloadToken = application.CreateWorkloadToken("lobby.write");

        using var response = await SendJsonAsync(client, HttpMethod.Post, "/lobbies", workloadToken, new
        {
            capacity = 2,
            gameId = "dota2",
            minimumRankOrdinal = 1
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemCodeAsync(response, "delegated_user_required");
    }

    private static async Task AssertMemberOwnedByAuthenticatedSubjectAsync(
        LobbyApplication application,
        Guid lobbyId,
        Guid playerId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LobbyDbContext>();
        var lobby = await context.Lobbies
            .Include("members")
            .SingleAsync(candidate => candidate.Id == lobbyId);
        Assert.Contains(lobby.Members, member => member.PlayerId == playerId);
    }

    private static async Task AssertProblemCodeAsync(HttpResponseMessage response, string code)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains($"\"code\":\"{code}\"", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? token)
    {
        var request = new HttpRequestMessage(method, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendJsonAsync(HttpClient client, HttpMethod method, string path, string? token, object body)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client.SendAsync(request);
    }

    private sealed class LobbyApplication : WebApplicationFactory<Program>
    {
        private readonly string connectionString;
        private readonly string privateKeyPem;
        private readonly string publicKeyPem;

        public LobbyApplication(string connectionString)
        {
            this.connectionString = connectionString;
            using var rsa = RSA.Create(2048);
            privateKeyPem = rsa.ExportRSAPrivateKeyPem();
            publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();
        }

        public async Task MigrateAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<LobbyDbContext>().Database.MigrateAsync();
        }

        public string CreateDelegatedToken(Guid playerId, string scope) => CreateToken(playerId.ToString("D"), "delegated_user", scope);

        public string CreateWorkloadToken(string scope) => CreateToken(ClientId, "workload", scope);

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LobbyDatabase"] = connectionString,
                ["InternalAuthentication:Issuer"] = Issuer,
                ["InternalAuthentication:Audience"] = Audience,
                ["InternalAuthentication:ApiClientId"] = ClientId,
                ["InternalAuthentication:MaximumTokenLifetimeSeconds"] = "120",
                ["InternalAuthentication:AllowedScopes:0"] = "lobby.read",
                ["InternalAuthentication:AllowedScopes:1"] = "lobby.write",
                [$"InternalAuthentication:PublicKeys:{KeyId}"] = publicKeyPem
            }));

        private string CreateToken(string subject, string tokenKind, string scope)
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);
            var key = new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: true)) { KeyId = KeyId };
            var now = DateTime.UtcNow;
            return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = Issuer,
                Audience = Audience,
                Subject = new ClaimsIdentity(
                [
                    new Claim(JwtRegisteredClaimNames.Sub, subject),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString("D")),
                    new Claim("client_id", ClientId),
                    new Claim("scope", scope),
                    new Claim("token_kind", tokenKind)
                ]),
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(2),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
            });
        }
    }
}
