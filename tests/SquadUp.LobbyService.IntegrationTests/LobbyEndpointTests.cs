using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SquadUp.LobbyService.Application;
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

        using var anonymousSearch = await SendAsync(client, HttpMethod.Get, "/lobbies", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousSearch.StatusCode);
        await AssertProblemCodeAsync(anonymousSearch, "authentication_required");

        using var create = await SendJsonAsync(client, HttpMethod.Post, "/lobbies", ownerToken, new
        {
            capacity = 2,
            gameId = "dota2",
            minimumRankOrdinal = 1,
            ownerPlayerId = otherPlayerId
        }, idempotencyKey: "endpoint-create-1");
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var lobbyId = created.RootElement.GetProperty("lobbyId").GetGuid();

        using var search = await SendAsync(client, HttpMethod.Get, "/lobbies?gameId=dota2&pageSize=1", ownerToken);
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var searchBody = await search.Content.ReadAsStringAsync();
        Assert.Contains(lobbyId.ToString("D"), searchBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ownerPlayerId", searchBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"members\":", searchBody, StringComparison.Ordinal);

        using var invalidCursor = await SendAsync(client, HttpMethod.Get, "/lobbies?cursor=not-a-cursor", ownerToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCursor.StatusCode);
        await AssertProblemCodeAsync(invalidCursor, "lobby_cursor_invalid");

        using var invalidPageSize = await SendAsync(client, HttpMethod.Get, "/lobbies?pageSize=51", ownerToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPageSize.StatusCode);
        await AssertProblemCodeAsync(invalidPageSize, "lobby_page_size_invalid");

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
        }, idempotencyKey: "endpoint-join-1");
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

    [Fact]
    public async Task IdempotencyKeysReplayTheStoredResponseRejectConflictsAndExpire()
    {
        await using var application = new LobbyApplication(fixture.PostgreSql.GetConnectionString());
        await application.MigrateAsync();
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var ownerId = Guid.CreateVersion7();
        var otherOwnerId = Guid.CreateVersion7();
        var ownerToken = application.CreateDelegatedToken(ownerId, "lobby.write");
        var otherOwnerToken = application.CreateDelegatedToken(otherOwnerId, "lobby.write");
        const string key = "idempotency-create-1";
        var body = new { capacity = 2, gameId = "dota2", minimumRankOrdinal = 1 };

        using var missing = await SendJsonAsync(client, HttpMethod.Post, "/lobbies", ownerToken, body);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        await AssertProblemCodeAsync(missing, "idempotency_key_required");

        using var first = await SendJsonAsync(client, HttpMethod.Post, "/lobbies", ownerToken, body, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstLobbyId = GetLobbyId(await first.Content.ReadAsStringAsync());

        using var replay = await SendJsonAsync(client, HttpMethod.Post, "/lobbies", ownerToken, body, key);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(firstLobbyId, GetLobbyId(await replay.Content.ReadAsStringAsync()));

        using var conflict = await SendJsonAsync(client, HttpMethod.Post, "/lobbies", ownerToken, new
        {
            capacity = 3,
            gameId = "dota2",
            minimumRankOrdinal = 1
        }, key);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        await AssertProblemCodeAsync(conflict, "idempotency_key_conflict");

        using var distinctOwner = await SendJsonAsync(client, HttpMethod.Post, "/lobbies", otherOwnerToken, body, key);
        Assert.Equal(HttpStatusCode.Created, distinctOwner.StatusCode);
        Assert.NotEqual(firstLobbyId, GetLobbyId(await distinctOwner.Content.ReadAsStringAsync()));

        await ExpireKeyAsync(application, ownerId, key);
        using var afterExpiry = await SendJsonAsync(client, HttpMethod.Post, "/lobbies", ownerToken, body, key);
        Assert.Equal(HttpStatusCode.Created, afterExpiry.StatusCode);
        Assert.NotEqual(firstLobbyId, GetLobbyId(await afterExpiry.Content.ReadAsStringAsync()));

        var concurrentKey = "idempotency-create-concurrent";
        var concurrentFirst = SendJsonAsync(client, HttpMethod.Post, "/lobbies", ownerToken, body, concurrentKey);
        var concurrentSecond = SendJsonAsync(client, HttpMethod.Post, "/lobbies", ownerToken, body, concurrentKey);
        var concurrentResponses = await Task.WhenAll(concurrentFirst, concurrentSecond);
        using var concurrentResponseOne = concurrentResponses[0];
        using var concurrentResponseTwo = concurrentResponses[1];
        Assert.Equal(HttpStatusCode.Created, concurrentResponseOne.StatusCode);
        Assert.Equal(HttpStatusCode.Created, concurrentResponseTwo.StatusCode);
        Assert.Equal(
            GetLobbyId(await concurrentResponseOne.Content.ReadAsStringAsync()),
            GetLobbyId(await concurrentResponseTwo.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task JoinRequiresAnIdempotencyKeyAndReplaysWithoutAddingAnotherMember()
    {
        await using var application = new LobbyApplication(fixture.PostgreSql.GetConnectionString());
        await application.MigrateAsync();
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var ownerId = Guid.CreateVersion7();
        var playerId = Guid.CreateVersion7();
        var ownerToken = application.CreateDelegatedToken(ownerId, "lobby.write");
        var playerToken = application.CreateDelegatedToken(playerId, "lobby.write");
        using var create = await SendJsonAsync(client, HttpMethod.Post, "/lobbies", ownerToken, new
        {
            capacity = 2,
            gameId = "dota2",
            minimumRankOrdinal = 1
        }, "join-test-create");
        var lobbyId = GetLobbyId(await create.Content.ReadAsStringAsync());
        var body = new { discordUserId = "synthetic-discord-id", displayName = "Synthetic Player", gameId = "dota2", rankOrdinal = 1 };

        using var missing = await SendJsonAsync(client, HttpMethod.Post, $"/lobbies/{lobbyId}/members", playerToken, body);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        await AssertProblemCodeAsync(missing, "idempotency_key_required");

        using var first = await SendJsonAsync(client, HttpMethod.Post, $"/lobbies/{lobbyId}/members", playerToken, body, "join-test-1");
        using var replay = await SendJsonAsync(client, HttpMethod.Post, $"/lobbies/{lobbyId}/members", playerToken, body, "join-test-1");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);
        await AssertMemberOwnedByAuthenticatedSubjectAsync(application, lobbyId, playerId);
    }

    [Fact]
    public async Task HybridCacheUsesL1AndRedisL2ForAnAllowlistedProjectionAndRedisOutageDoesNotBlockJoin()
    {
        var cacheKey = $"f4-01:cache-projection:{Guid.CreateVersion7():D}";
        await using (var firstApplication = new LobbyApplication(
            fixture.PostgreSql.GetConnectionString(),
            fixture.Redis.GetConnectionString()))
        {
            var firstCache = firstApplication.Services.GetRequiredService<ILobbyReadCache>();
            var distributedCache = firstApplication.Services.GetRequiredService<IDistributedCache>();
            Assert.Contains("RedisCache", distributedCache.GetType().Name, StringComparison.Ordinal);
            var factoryCalls = 0;
            var first = await firstCache.GetOrCreateAsync(
                cacheKey,
                _ =>
                {
                    factoryCalls++;
                    return ValueTask.FromResult(new CacheProbeProjection("allowlisted"));
                },
                null,
                CancellationToken.None);
            var l1 = await firstCache.GetOrCreateAsync(
                cacheKey,
                _ => ValueTask.FromException<CacheProbeProjection>(new InvalidOperationException("L1 cache miss")),
                null,
                CancellationToken.None);

            Assert.Equal("allowlisted", first.Value);
            Assert.Equal(first, l1);
            Assert.Equal(1, factoryCalls);
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            Assert.NotNull(await distributedCache.GetAsync(cacheKey));
        }

        await using (var secondApplication = new LobbyApplication(
            fixture.PostgreSql.GetConnectionString(),
            fixture.Redis.GetConnectionString()))
        {
            var secondCache = secondApplication.Services.GetRequiredService<ILobbyReadCache>();
            var l2 = await secondCache.GetOrCreateAsync(
                cacheKey,
                _ => ValueTask.FromException<CacheProbeProjection>(new InvalidOperationException("L2 cache miss")),
                null,
                CancellationToken.None);

            Assert.Equal("allowlisted", l2.Value);
        }

        await using var offlineApplication = new LobbyApplication(
            fixture.PostgreSql.GetConnectionString(),
            "localhost:1,abortConnect=false,connectTimeout=25,syncTimeout=25");
        await offlineApplication.MigrateAsync();
        using var client = offlineApplication.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var ownerId = Guid.CreateVersion7();
        var playerId = Guid.CreateVersion7();
        var ownerToken = offlineApplication.CreateDelegatedToken(ownerId, "lobby.write");
        var playerToken = offlineApplication.CreateDelegatedToken(playerId, "lobby.write");
        using var create = await SendJsonAsync(client, HttpMethod.Post, "/lobbies", ownerToken, new
        {
            capacity = 2,
            gameId = "dota2",
            minimumRankOrdinal = 1
        }, "redis-outage-create");
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var lobbyId = GetLobbyId(await create.Content.ReadAsStringAsync());

        using var join = await SendJsonAsync(client, HttpMethod.Post, $"/lobbies/{lobbyId}/members", playerToken, new
        {
            discordUserId = "synthetic-discord-id",
            displayName = "Synthetic Player",
            gameId = "dota2",
            rankOrdinal = 1
        }, "redis-outage-join");
        Assert.Equal(HttpStatusCode.NoContent, join.StatusCode);
    }

    [Fact]
    public async Task SearchCacheIsIsolatedByPageAndInvalidatedLocallyAfterPersistedMutations()
    {
        await using var application = new LobbyApplication(
            fixture.PostgreSql.GetConnectionString(),
            fixture.Redis.GetConnectionString());
        await application.MigrateAsync();
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var ownerId = Guid.CreateVersion7();
        var playerId = Guid.CreateVersion7();
        var readToken = application.CreateDelegatedToken(ownerId, "lobby.read");
        var writeToken = application.CreateDelegatedToken(ownerId, "lobby.write");
        var playerToken = application.CreateDelegatedToken(playerId, "lobby.write");

        Guid sentinelLobbyId;
        using (var sentinel = await SendJsonAsync(client, HttpMethod.Post, "/lobbies", writeToken, new
        {
            capacity = 3,
            gameId = "dota2",
            minimumRankOrdinal = 1
        }, "search-cache-sentinel"))
        {
            Assert.Equal(HttpStatusCode.Created, sentinel.StatusCode);
            sentinelLobbyId = GetLobbyId(await sentinel.Content.ReadAsStringAsync());
        }

        var searchPath = $"/lobbies?gameId=dota2&cursor={LobbySearchCursor.Encode(sentinelLobbyId)}&pageSize=";
        using var initiallyEmpty = await SendAsync(client, HttpMethod.Get, searchPath + "1", readToken);
        Assert.Equal(HttpStatusCode.OK, initiallyEmpty.StatusCode);
        Assert.Empty(GetSearchItems(await initiallyEmpty.Content.ReadAsStringAsync()));

        for (var index = 0; index < 2; index++)
        {
            using var create = await SendJsonAsync(client, HttpMethod.Post, "/lobbies", writeToken, new
            {
                capacity = 3,
                gameId = "dota2",
                minimumRankOrdinal = 1
            }, $"search-cache-create-{index}");
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }

        using var pageOne = await SendAsync(client, HttpMethod.Get, searchPath + "1", readToken);
        using var pageTwo = await SendAsync(client, HttpMethod.Get, searchPath + "2", readToken);
        var pageOneItems = GetSearchItems(await pageOne.Content.ReadAsStringAsync());
        var pageTwoItems = GetSearchItems(await pageTwo.Content.ReadAsStringAsync());
        Assert.Single(pageOneItems);
        Assert.Equal(2, pageTwoItems.Count);

        var joinedLobbyId = pageOneItems[0].GetProperty("lobbyId").GetGuid();
        using var join = await SendJsonAsync(client, HttpMethod.Post, $"/lobbies/{joinedLobbyId}/members", playerToken, new
        {
            discordUserId = "synthetic-discord-id",
            displayName = "Synthetic Player",
            gameId = "dota2",
            rankOrdinal = 1
        }, "search-cache-join");
        Assert.Equal(HttpStatusCode.NoContent, join.StatusCode);

        using var refreshed = await SendAsync(client, HttpMethod.Get, searchPath + "1", readToken);
        var refreshedItem = Assert.Single(GetSearchItems(await refreshed.Content.ReadAsStringAsync()));
        Assert.Equal(joinedLobbyId, refreshedItem.GetProperty("lobbyId").GetGuid());
        Assert.Equal(1, refreshedItem.GetProperty("membersCount").GetInt32());
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

    private static Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string? token,
        object body,
        string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return client.SendAsync(request);
    }

    private static Guid GetLobbyId(string content) => JsonDocument.Parse(content).RootElement.GetProperty("lobbyId").GetGuid();

    private static List<JsonElement> GetSearchItems(string content) => JsonDocument.Parse(content)
        .RootElement
        .GetProperty("items")
        .EnumerateArray()
        .Select(item => item.Clone())
        .ToList();

    private static async Task ExpireKeyAsync(LobbyApplication application, Guid ownerId, string key)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LobbyDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE lobby.http_idempotency_keys SET expires_at_utc = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE owner_player_id = {ownerId} AND key = {key}");
    }

    private sealed class LobbyApplication : WebApplicationFactory<Program>
    {
        private readonly string connectionString;
        private readonly string? redisConnectionString;
        private readonly string privateKeyPem;
        private readonly string publicKeyPem;

        public LobbyApplication(string connectionString, string? redisConnectionString = null)
        {
            this.connectionString = connectionString;
            this.redisConnectionString = redisConnectionString;
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
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:LobbyDatabase"] = connectionString,
                ["InternalAuthentication:Issuer"] = Issuer,
                ["InternalAuthentication:Audience"] = Audience,
                ["InternalAuthentication:ApiClientId"] = ClientId,
                ["InternalAuthentication:MaximumTokenLifetimeSeconds"] = "120",
                ["InternalAuthentication:AllowedScopes:0"] = "lobby.read",
                ["InternalAuthentication:AllowedScopes:1"] = "lobby.write",
                [$"InternalAuthentication:PublicKeys:{KeyId}"] = publicKeyPem
            };
            if (!string.IsNullOrWhiteSpace(redisConnectionString))
            {
                values["ConnectionStrings:LobbyCache"] = redisConnectionString;
            }

            configuration.AddInMemoryCollection(values);
        });

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

public sealed record CacheProbeProjection(string Value) : IAllowlistedLobbyReadProjection;
