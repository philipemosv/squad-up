using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SquadUp.Identity.Application;
using SquadUp.Identity.Infrastructure;
using SquadUp.Profile.Infrastructure;

namespace SquadUp.Api.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class ProfileEndpointTests : IClassFixture<ProfileDatabaseFixture>
{
    private readonly ProfileDatabaseFixture fixture;

    public ProfileEndpointTests(ProfileDatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task UnauthenticatedRequestsToMeEndpointsAreRejected()
    {
        await MigrateAsync();
        await using var application = CreateApplication();
        using var client = CreateClient(application);

        using var profile = await client.GetAsync("/me/profile");
        using var games = await client.GetAsync("/me/games");
        using var putProfile = await SendAnonymousJsonAsync(
            client,
            HttpMethod.Put,
            "/me/profile",
            new { nickname = "Alpha", timeZoneId = (string?)null });
        using var putGame = await SendAnonymousJsonAsync(
            client,
            HttpMethod.Put,
            "/me/games/dota2",
            new { rankTierId = "immortal", region = "SA" });
        using var deleteGame = await client.DeleteAsync("/me/games/dota2");

        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, games.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, putProfile.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, putGame.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deleteGame.StatusCode);
    }

    [Fact]
    public async Task CookieAuthenticatedMutationsRequireAntiforgery()
    {
        await MigrateAsync();
        await using var application = CreateApplication();
        using var client = CreateClient(application);
        var session = await LoginAsync(client, application, "777777777777777777");

        using var putProfile = await SendAsync(
            client,
            HttpMethod.Put,
            "/me/profile",
            session,
            new { nickname = "Alpha", timeZoneId = (string?)null },
            includeAntiforgery: false);
        using var putGame = await SendAsync(
            client,
            HttpMethod.Put,
            "/me/games/dota2",
            session,
            new { rankTierId = "immortal", region = "SA" },
            includeAntiforgery: false);
        using var deleteGameRequest = WithCookie(
            new HttpRequestMessage(HttpMethod.Delete, "/me/games/dota2"),
            session,
            includeAntiforgery: false);
        using var deleteGame = await client.SendAsync(deleteGameRequest);

        await AssertAntiforgeryFailureAsync(putProfile);
        await AssertAntiforgeryFailureAsync(putGame);
        await AssertAntiforgeryFailureAsync(deleteGame);
    }

    [Fact]
    public async Task CatalogEndpointsArePublicAndServeTheSeededDota2Data()
    {
        await MigrateAsync();
        await using var application = CreateApplication();
        using var client = CreateClient(application);

        using var games = await client.GetAsync("/catalog/games");
        using var ranks = await client.GetAsync("/catalog/games/dota2/ranks");

        Assert.Equal(HttpStatusCode.OK, games.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ranks.StatusCode);
        using var gamesDocument = JsonDocument.Parse(await games.Content.ReadAsStringAsync());
        using var ranksDocument = JsonDocument.Parse(await ranks.Content.ReadAsStringAsync());
        Assert.Single(gamesDocument.RootElement.EnumerateArray(), element =>
            element.GetProperty("gameId").GetString() == "dota2");
        Assert.Equal(8, ranksDocument.RootElement.GetArrayLength());
        Assert.Equal("herald", ranksDocument.RootElement[0].GetProperty("tierId").GetString());
        Assert.Equal("immortal", ranksDocument.RootElement[7].GetProperty("tierId").GetString());
    }

    [Fact]
    public async Task AuthenticatedPlayerCanCreateThenReadTheirOwnProfile()
    {
        await MigrateAsync();
        await using var application = CreateApplication();
        using var client = CreateClient(application);
        var session = await LoginAsync(client, application, "111111111111111111");

        using var put = await SendAsync(client, HttpMethod.Put, "/me/profile", session,
            new { nickname = "Alpha", timeZoneId = (string?)null });
        using var get = await SendAsync(client, HttpMethod.Get, "/me/profile", session);
        var body = await get.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Contains("\"nickname\":\"Alpha\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoAuthenticatedPlayersCannotSeeOrOverwriteEachOthersProfile()
    {
        await MigrateAsync();
        await using var application = CreateApplication();
        using var client = CreateClient(application);
        var alphaSession = await LoginAsync(client, application, "222222222222222222");
        var bravoSession = await LoginAsync(client, application, "333333333333333333");

        using var alphaPut = await SendAsync(client, HttpMethod.Put, "/me/profile", alphaSession,
            new { nickname = "Alpha", timeZoneId = (string?)null });
        using var alphaPutDocument = JsonDocument.Parse(await alphaPut.Content.ReadAsStringAsync());
        var alphaPlayerId = alphaPutDocument.RootElement.GetProperty("playerId").GetString();
        using var bravoPut = await SendAsync(
            client,
            HttpMethod.Put,
            "/me/profile",
            bravoSession,
            // playerId is not a bindable field: this proves the server never trusts a
            // client-supplied identity and always resolves ownership from the session claim.
            new { nickname = "Bravo", timeZoneId = (string?)null, playerId = alphaPlayerId });

        using var alphaGet = await SendAsync(client, HttpMethod.Get, "/me/profile", alphaSession);
        using var bravoGet = await SendAsync(client, HttpMethod.Get, "/me/profile", bravoSession);
        var alphaBody = await alphaGet.Content.ReadAsStringAsync();
        var bravoBody = await bravoGet.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, bravoPut.StatusCode);
        Assert.Contains("\"nickname\":\"Alpha\"", alphaBody, StringComparison.Ordinal);
        Assert.Contains("\"nickname\":\"Bravo\"", bravoBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Bravo", alphaBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Alpha", bravoBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdatingAnExistingProfileWithoutAVersionIsRejectedWithConflict()
    {
        await MigrateAsync();
        await using var application = CreateApplication();
        using var client = CreateClient(application);
        var session = await LoginAsync(client, application, "444444444444444444");
        await SendAsync(client, HttpMethod.Put, "/me/profile", session,
            new { nickname = "Alpha", timeZoneId = (string?)null });

        using var response = await SendAsync(client, HttpMethod.Put, "/me/profile", session,
            new { nickname = "AlphaRenamed", timeZoneId = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PlayerGameEndpointsRejectUnknownGamesAndRankTiers()
    {
        await MigrateAsync();
        await using var application = CreateApplication();
        using var client = CreateClient(application);
        var session = await LoginAsync(client, application, "555555555555555555");
        await SendAsync(client, HttpMethod.Put, "/me/profile", session,
            new { nickname = "Alpha", timeZoneId = (string?)null });

        using var unknownGame = await SendAsync(client, HttpMethod.Put, "/me/games/not-a-game", session,
            new { rankTierId = "immortal", region = "SA" });
        using var unknownRank = await SendAsync(client, HttpMethod.Put, "/me/games/dota2", session,
            new { rankTierId = "not-a-rank", region = "SA" });

        Assert.Equal(HttpStatusCode.NotFound, unknownGame.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownRank.StatusCode);
    }

    [Fact]
    public async Task PlayerCanAddListAndRemoveAGame()
    {
        await MigrateAsync();
        await using var application = CreateApplication();
        using var client = CreateClient(application);
        var session = await LoginAsync(client, application, "666666666666666666");
        await SendAsync(client, HttpMethod.Put, "/me/profile", session,
            new { nickname = "Alpha", timeZoneId = (string?)null });

        using var put = await SendAsync(client, HttpMethod.Put, "/me/games/dota2", session,
            new { rankTierId = "immortal", region = "sa" });
        using var list = await SendAsync(client, HttpMethod.Get, "/me/games", session);
        using var delete = await client.SendAsync(WithCookie(
            new HttpRequestMessage(HttpMethod.Delete, "/me/games/dota2"), session));
        using var listAfterDelete = await SendAsync(client, HttpMethod.Get, "/me/games", session);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var listBody = await list.Content.ReadAsStringAsync();
        Assert.Contains("\"gameId\":\"dota2\"", listBody, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal("[]", await listAfterDelete.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TwoAuthenticatedPlayersCannotSeeOrDeleteEachOthersGames()
    {
        await MigrateAsync();
        await using var application = CreateApplication();
        using var client = CreateClient(application);
        var alphaSession = await LoginAsync(client, application, "888888888888888888");
        var bravoSession = await LoginAsync(client, application, "999999999999999999");
        await SendAsync(client, HttpMethod.Put, "/me/profile", alphaSession,
            new { nickname = "Alpha", timeZoneId = (string?)null });
        await SendAsync(client, HttpMethod.Put, "/me/profile", bravoSession,
            new { nickname = "Bravo", timeZoneId = (string?)null });
        await SendAsync(client, HttpMethod.Put, "/me/games/dota2", alphaSession,
            new { rankTierId = "immortal", region = "SA" });

        using var bravoList = await SendAsync(client, HttpMethod.Get, "/me/games", bravoSession);
        using var bravoDeleteRequest = WithCookie(
            new HttpRequestMessage(HttpMethod.Delete, "/me/games/dota2"),
            bravoSession);
        using var bravoDelete = await client.SendAsync(bravoDeleteRequest);
        using var alphaList = await SendAsync(client, HttpMethod.Get, "/me/games", alphaSession);

        Assert.Equal("[]", await bravoList.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, bravoDelete.StatusCode);
        Assert.Contains(
            "\"gameId\":\"dota2\"",
            await alphaList.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    private async Task MigrateAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ProfileDatabase"] = fixture.PostgreSql.GetConnectionString()
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProfileInfrastructure(configuration);
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProfileDbContext>().Database.MigrateAsync();
    }

    private ProfileApplication CreateApplication() => new(fixture.PostgreSql.GetConnectionString());

    private static HttpClient CreateClient(WebApplicationFactory<Program> application) =>
        application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });

    private static async Task<AuthenticatedSession> LoginAsync(
        HttpClient client,
        ProfileApplication application,
        string discordUserId)
    {
        application.CurrentDiscordUserId = discordUserId;
        using var login = await client.GetAsync("/auth/discord/login");
        var location = Assert.IsType<Uri>(login.Headers.Location);
        var state = Assert.Single(QueryHelpers.ParseQuery(location.Query)["state"])!;
        var correlation = GetCookie(login, "__Host-SquadUp.Correlation.");

        using var callbackRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/auth/discord/callback?code=synthetic-code&state={Uri.EscapeDataString(state)}");
        callbackRequest.Headers.Add("Cookie", correlation.Pair);
        using var callback = await client.SendAsync(callbackRequest);
        var externalCookie = GetCookie(callback, "__Host-SquadUp.External");

        using var completionRequest = new HttpRequestMessage(HttpMethod.Get, DiscordOAuthDefaults.CompletionPath);
        completionRequest.Headers.Add("Cookie", externalCookie.Pair);
        using var completion = await client.SendAsync(completionRequest);
        Assert.Equal(HttpStatusCode.NoContent, completion.StatusCode);

        var sessionCookie = GetCookie(completion, BrowserSessionExtensions.SessionCookieName);
        using var antiforgeryRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/antiforgery");
        antiforgeryRequest.Headers.Add("Cookie", sessionCookie.Pair);
        using var antiforgeryResponse = await client.SendAsync(antiforgeryRequest);
        Assert.Equal(HttpStatusCode.OK, antiforgeryResponse.StatusCode);
        using var antiforgeryDocument = JsonDocument.Parse(
            await antiforgeryResponse.Content.ReadAsStringAsync());
        var requestToken = antiforgeryDocument.RootElement.GetProperty("requestToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(requestToken));

        return new AuthenticatedSession(
            sessionCookie,
            GetCookie(antiforgeryResponse, BrowserSessionExtensions.AntiforgeryCookieName),
            requestToken!);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        AuthenticatedSession session,
        object? body = null,
        bool includeAntiforgery = true)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");
        }

        return await client.SendAsync(WithCookie(request, session, includeAntiforgery));
    }

    private static async Task<HttpResponseMessage> SendAnonymousJsonAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object body)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage WithCookie(
        HttpRequestMessage request,
        AuthenticatedSession session,
        bool includeAntiforgery = true)
    {
        var cookieHeader = includeAntiforgery
            ? $"{session.SessionCookie.Pair}; {session.AntiforgeryCookie.Pair}"
            : session.SessionCookie.Pair;
        request.Headers.Add("Cookie", cookieHeader);
        if (includeAntiforgery)
        {
            request.Headers.Add(BrowserSessionExtensions.AntiforgeryHeaderName, session.RequestToken);
        }

        return request;
    }

    private static async Task AssertAntiforgeryFailureAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            "antiforgery_validation_failed",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    private static CookieValue GetCookie(HttpResponseMessage response, string namePrefix)
    {
        var header = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(namePrefix, StringComparison.Ordinal));
        var pair = header.Split(';', 2)[0];
        var separator = pair.IndexOf('=', StringComparison.Ordinal);

        return new CookieValue(pair[..separator], pair, header);
    }

    private sealed record CookieValue(string Name, string Pair, string Header);

    private sealed record AuthenticatedSession(
        CookieValue SessionCookie,
        CookieValue AntiforgeryCookie,
        string RequestToken);

    private sealed class ProfileApplication : WebApplicationFactory<Program>
    {
        private readonly string profileConnectionString;

        public ProfileApplication(string profileConnectionString)
        {
            this.profileConnectionString = profileConnectionString;
            ClientSecret = RandomNumberGenerator.GetHexString(32);
            using var rsa = RSA.Create(2048);
            PrivateKeyPem = rsa.ExportRSAPrivateKeyPem();
        }

        public string ClientSecret { get; }

        public string PrivateKeyPem { get; }

        public string CurrentDiscordUserId { get; set; } = "100000000000000000";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:IdentityDatabase"] =
                        "Host=127.0.0.1;Port=1;Database=unavailable;Timeout=1",
                    ["ConnectionStrings:ProfileDatabase"] = profileConnectionString,
                    ["Discord:ClientId"] = "123456789012345678",
                    ["Discord:ClientSecret"] = ClientSecret,
                    ["InternalTokens:Issuer"] = "https://api.squad-up.test",
                    ["InternalTokens:LobbyAudience"] = "squad-up-lobby",
                    ["InternalTokens:ClientId"] = "squad-up-api",
                    ["InternalTokens:LifetimeSeconds"] = "120",
                    ["InternalTokens:ActiveKeyId"] = "test-current",
                    ["InternalTokens:PrivateKeyPem"] = PrivateKeyPem,
                    ["InternalTokens:AllowedScopes:0"] = "lobby.read",
                    ["InternalTokens:AllowedScopes:1"] = "lobby.write"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<OAuthOptions>(
                    DiscordOAuthDefaults.AuthenticationScheme,
                    options => options.Backchannel = new HttpClient(
                        new StubBackchannelHandler(this),
                        disposeHandler: false));
                services.RemoveAll<IExternalLoginAccountService>();
                services.AddSingleton<IExternalLoginAccountService>(new StubExternalLoginAccountService());
                services.RemoveAll<IUserSessionClaimsProvider>();
                services.AddSingleton<IUserSessionClaimsProvider>(new StubUserSessionClaimsProvider());
            });
        }
    }

    private sealed class StubExternalLoginAccountService : IExternalLoginAccountService
    {
        private readonly Dictionary<string, Guid> playerIdsByProviderKey = [];

        public Task<ExternalLoginUpsertResult> UpsertAsync(
            string loginProvider,
            string providerKey,
            CancellationToken cancellationToken)
        {
            if (!playerIdsByProviderKey.TryGetValue(providerKey, out var playerId))
            {
                playerId = Guid.CreateVersion7();
                playerIdsByProviderKey[providerKey] = playerId;
            }

            return Task.FromResult(new ExternalLoginUpsertResult(playerId, WasCreated: true));
        }

        public Task<ExternalLoginLinkResult> LinkAsync(
            Guid userId,
            string loginProvider,
            string providerKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ExternalLoginUnlinkResult> UnlinkAsync(
            Guid userId,
            string loginProvider,
            string providerKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubUserSessionClaimsProvider : IUserSessionClaimsProvider
    {
        public Task<UserSessionClaims?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserSessionClaims?>(new UserSessionClaims(userId, [SquadUpRoles.Player]));
    }

    private sealed class StubBackchannelHandler(ProfileApplication application) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri == new Uri(DiscordOAuthDefaults.TokenEndpoint))
            {
                return Task.FromResult(JsonResponse(
                    """{"access_token":"stub-access-token","token_type":"Bearer","expires_in":3600,"scope":"identify"}"""));
            }

            if (request.RequestUri == new Uri(DiscordOAuthDefaults.UserInformationEndpoint))
            {
                return Task.FromResult(JsonResponse(
                    $$"""{"id":"{{application.CurrentDiscordUserId}}","username":"synthetic-user"}"""));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }
}
