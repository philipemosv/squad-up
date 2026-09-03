using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SquadUp.Identity.Application;
using SquadUp.Identity.Infrastructure;

namespace SquadUp.Api.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class DiscordOAuthTests
{
    private const string ClientId = "123456789012345678";
    private const string AuthorizationCode = "synthetic-authorization-code";
    private const string DiscordUserId = "987654321098765432";

    [Fact]
    public async Task LoginUsesFixedDiscordEndpointExactCallbackAndIdentifyScope()
    {
        using var backchannel = new DiscordBackchannelHandler();
        await using var application = new DiscordApplication(backchannel);
        using var client = CreateClient(application);

        using var response = await client.GetAsync("/auth/discord/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("discord.com", location.Host);
        Assert.Equal("/oauth2/authorize", location.AbsolutePath);

        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("code", Assert.Single(query["response_type"]));
        Assert.Equal(ClientId, Assert.Single(query["client_id"]));
        Assert.Equal(DiscordOAuthDefaults.Scope, Assert.Single(query["scope"]));
        Assert.Equal(
            "https://localhost/auth/discord/callback",
            Assert.Single(query["redirect_uri"]));
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(query["state"])));

        var correlationCookie = GetCookie(response, "__Host-SquadUp.Correlation.");
        Assert.Contains("; secure", correlationCookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; httponly", correlationCookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; samesite=lax", correlationCookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(application.ClientSecret, correlationCookie.Header, StringComparison.Ordinal);
        Assert.Equal(0, backchannel.RequestCount);
    }

    [Fact]
    public async Task ValidCallbackUpsertsExternalLoginAndIssuesBoundedBrowserSession()
    {
        using var backchannel = new DiscordBackchannelHandler();
        await using var application = new DiscordApplication(backchannel);
        using var client = CreateClient(application);
        using var login = await client.GetAsync("/auth/discord/login");
        var location = Assert.IsType<Uri>(login.Headers.Location);
        var state = Assert.Single(QueryHelpers.ParseQuery(location.Query)["state"]);
        var correlation = GetCookie(login, "__Host-SquadUp.Correlation.");

        using var callbackRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/auth/discord/callback?code={Uri.EscapeDataString(AuthorizationCode)}&state={Uri.EscapeDataString(state!)}");
        callbackRequest.Headers.Add("Cookie", correlation.Pair);
        using var callback = await client.SendAsync(callbackRequest);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal(DiscordOAuthDefaults.CompletionPath, callback.Headers.Location?.OriginalString);
        var externalCookie = GetCookie(callback, "__Host-SquadUp.External");
        var callbackHeaders = callback.Headers.ToString();
        Assert.DoesNotContain(backchannel.AccessToken, callbackHeaders, StringComparison.Ordinal);
        Assert.DoesNotContain(application.ClientSecret, callbackHeaders, StringComparison.Ordinal);
        Assert.Contains(
            callback.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(correlation.Name + "=", StringComparison.Ordinal) &&
                value.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));

        using var completionRequest = new HttpRequestMessage(
            HttpMethod.Get,
            DiscordOAuthDefaults.CompletionPath);
        completionRequest.Headers.Add("Cookie", externalCookie.Pair);
        using var completion = await client.SendAsync(completionRequest);
        var completionBody = await completion.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NoContent, completion.StatusCode);
        Assert.Empty(completionBody);
        var sessionCookie = GetCookie(completion, BrowserSessionExtensions.SessionCookieName);
        Assert.Contains("; secure", sessionCookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; httponly", sessionCookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; samesite=lax", sessionCookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; path=/", sessionCookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", sessionCookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(backchannel.AccessToken, sessionCookie.Header, StringComparison.Ordinal);
        Assert.DoesNotContain(DiscordUserId, sessionCookie.Header, StringComparison.Ordinal);
        Assert.Contains(
            completion.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-SquadUp.External=", StringComparison.Ordinal) &&
                value.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(backchannel.AccessToken, completion.Headers.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(DiscordUserId, completion.Headers.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, backchannel.TokenRequestCount);
        Assert.Equal(1, backchannel.UserInformationRequestCount);
        Assert.Equal(1, application.ExternalLogins.UpsertCount);
        Assert.Equal(DiscordOAuthDefaults.AuthenticationScheme, application.ExternalLogins.LoginProvider);
        Assert.Equal(DiscordUserId, application.ExternalLogins.ProviderKey);

        var cookieOptions = application.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(BrowserSessionExtensions.AuthenticationScheme);
        Assert.Equal(TimeSpan.FromMinutes(30), cookieOptions.ExpireTimeSpan);
        Assert.False(cookieOptions.SlidingExpiration);
        var protectedTicket = Uri.UnescapeDataString(
            sessionCookie.Pair[(sessionCookie.Pair.IndexOf('=', StringComparison.Ordinal) + 1)..]);
        var ticket = cookieOptions.TicketDataFormat.Unprotect(protectedTicket);
        Assert.NotNull(ticket);
        Assert.Equal(
            application.ExternalLogins.UserId.ToString("D"),
            ticket.Principal.FindFirst(SquadUpClaimTypes.Subject)?.Value);
        Assert.True(ticket.Principal.IsInRole(SquadUpRoles.Player));
        Assert.False(ticket.Principal.IsInRole(SquadUpRoles.Admin));
        Assert.Null(ticket.Principal.FindFirst(SquadUpClaimTypes.DiscordUserId));
    }

    [Fact]
    public async Task CookieAuthenticatedLogoutRequiresAntiforgeryAndDeletesSession()
    {
        using var backchannel = new DiscordBackchannelHandler();
        await using var application = new DiscordApplication(backchannel);
        using var client = CreateClient(application);
        var session = await CompleteLoginAsync(client);

        using var missingAntiforgery = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        missingAntiforgery.Headers.Add("Cookie", session.Pair);
        using var rejected = await client.SendAsync(missingAntiforgery);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        using var antiforgeryRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/antiforgery");
        antiforgeryRequest.Headers.Add("Cookie", session.Pair);
        using var antiforgeryResponse = await client.SendAsync(antiforgeryRequest);
        using var document = JsonDocument.Parse(await antiforgeryResponse.Content.ReadAsStringAsync());
        var requestToken = document.RootElement.GetProperty("requestToken").GetString();
        var antiforgeryCookie = GetCookie(
            antiforgeryResponse,
            BrowserSessionExtensions.AntiforgeryCookieName);

        Assert.Equal(HttpStatusCode.OK, antiforgeryResponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(requestToken));
        Assert.Contains("; secure", antiforgeryCookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; httponly", antiforgeryCookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; samesite=strict", antiforgeryCookie.Header, StringComparison.OrdinalIgnoreCase);

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logout.Headers.Add("Cookie", $"{session.Pair}; {antiforgeryCookie.Pair}");
        logout.Headers.Add(BrowserSessionExtensions.AntiforgeryHeaderName, requestToken);
        using var loggedOut = await client.SendAsync(logout);

        Assert.Equal(HttpStatusCode.NoContent, loggedOut.StatusCode);
        Assert.Contains(
            loggedOut.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(BrowserSessionExtensions.SessionCookieName + "=", StringComparison.Ordinal) &&
                value.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UnauthenticatedBffEndpointReturnsUnauthorizedWithoutLoginRedirect()
    {
        using var backchannel = new DiscordBackchannelHandler();
        await using var application = new DiscordApplication(backchannel);
        using var client = CreateClient(application);

        using var response = await client.GetAsync("/auth/antiforgery");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task MissingOrAlteredCorrelationStateFailsBeforeTokenExchange(
        bool includeCorrelationCookie,
        bool alterState)
    {
        using var backchannel = new DiscordBackchannelHandler();
        await using var application = new DiscordApplication(backchannel);
        using var client = CreateClient(application);
        using var login = await client.GetAsync("/auth/discord/login");
        var location = Assert.IsType<Uri>(login.Headers.Location);
        var state = Assert.Single(QueryHelpers.ParseQuery(location.Query)["state"])!;
        var correlation = GetCookie(login, "__Host-SquadUp.Correlation.");
        var submittedState = alterState ? state + "altered" : state;

        using var callbackRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/auth/discord/callback?code={Uri.EscapeDataString(AuthorizationCode)}&state={Uri.EscapeDataString(submittedState)}");
        if (includeCorrelationCookie)
        {
            callbackRequest.Headers.Add("Cookie", correlation.Pair);
        }

        using var callback = await client.SendAsync(callbackRequest);
        var body = await callback.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, callback.StatusCode);
        Assert.Equal("application/problem+json", callback.Content.Headers.ContentType?.MediaType);
        Assert.Contains("discord_oauth_callback_invalid", body, StringComparison.Ordinal);
        Assert.DoesNotContain(AuthorizationCode, body, StringComparison.Ordinal);
        Assert.DoesNotContain(state, body, StringComparison.Ordinal);
        Assert.DoesNotContain(application.ClientSecret, body, StringComparison.Ordinal);
        Assert.Equal(0, backchannel.RequestCount);
    }

    [Theory]
    [InlineData(BackchannelFailure.TokenExchange, 1, 0)]
    [InlineData(BackchannelFailure.UserInformation, 1, 1)]
    public async Task ProviderFailureIsSanitizedAndIsNotRetried(
        BackchannelFailure failure,
        int expectedTokenRequests,
        int expectedUserRequests)
    {
        using var backchannel = new DiscordBackchannelHandler(failure);
        await using var application = new DiscordApplication(backchannel);
        using var client = CreateClient(application);
        using var login = await client.GetAsync("/auth/discord/login");
        var location = Assert.IsType<Uri>(login.Headers.Location);
        var state = Assert.Single(QueryHelpers.ParseQuery(location.Query)["state"])!;
        var correlation = GetCookie(login, "__Host-SquadUp.Correlation.");
        using var callbackRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/auth/discord/callback?code={Uri.EscapeDataString(AuthorizationCode)}&state={Uri.EscapeDataString(state)}");
        callbackRequest.Headers.Add("Cookie", correlation.Pair);

        using var callback = await client.SendAsync(callbackRequest);
        var body = await callback.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, callback.StatusCode);
        Assert.Contains("discord_oauth_callback_invalid", body, StringComparison.Ordinal);
        Assert.DoesNotContain(AuthorizationCode, body, StringComparison.Ordinal);
        Assert.DoesNotContain(backchannel.AccessToken, body, StringComparison.Ordinal);
        Assert.DoesNotContain(application.ClientSecret, body, StringComparison.Ordinal);
        Assert.Equal(expectedTokenRequests, backchannel.TokenRequestCount);
        Assert.Equal(expectedUserRequests, backchannel.UserInformationRequestCount);
    }

    [Fact]
    public async Task InvalidDiscordUserIdIsRejectedBeforeAccountUpsert()
    {
        const string invalidUserId = "not-a-discord-user-id";
        using var backchannel = new DiscordBackchannelHandler(userId: invalidUserId);
        await using var application = new DiscordApplication(backchannel);
        using var client = CreateClient(application);
        using var login = await client.GetAsync("/auth/discord/login");
        var location = Assert.IsType<Uri>(login.Headers.Location);
        var state = Assert.Single(QueryHelpers.ParseQuery(location.Query)["state"])!;
        var correlation = GetCookie(login, "__Host-SquadUp.Correlation.");
        using var callbackRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/auth/discord/callback?code={Uri.EscapeDataString(AuthorizationCode)}&state={Uri.EscapeDataString(state)}");
        callbackRequest.Headers.Add("Cookie", correlation.Pair);
        using var callback = await client.SendAsync(callbackRequest);
        var externalCookie = GetCookie(callback, "__Host-SquadUp.External");
        using var completionRequest = new HttpRequestMessage(
            HttpMethod.Get,
            DiscordOAuthDefaults.CompletionPath);
        completionRequest.Headers.Add("Cookie", externalCookie.Pair);

        using var completion = await client.SendAsync(completionRequest);
        var body = await completion.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, completion.StatusCode);
        Assert.Contains("discord_external_identity_invalid", body, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidUserId, body, StringComparison.Ordinal);
        Assert.Equal(0, application.ExternalLogins.UpsertCount);
        Assert.Contains(
            completion.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-SquadUp.External=", StringComparison.Ordinal) &&
                value.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InvalidConfigurationStopsStartupWithoutEchoingValues()
    {
        var validSecret = RandomNumberGenerator.GetHexString(32);
        (string? ClientId, string? ClientSecret)[] invalidConfigurations =
        {
            (null, null),
            ("not-numeric", validSecret),
            (ClientId, "too-short")
        };

        foreach (var (clientId, clientSecret) in invalidConfigurations)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Discord:ClientId"] = clientId,
                ["Discord:ClientSecret"] = clientSecret
            });
            builder.Services.AddDiscordOAuth(builder.Configuration);
            using var host = builder.Build();

            var exception = await Assert.ThrowsAsync<OptionsValidationException>(
                () => host.StartAsync(timeout.Token));

            Assert.Contains("Discord:", exception.Message, StringComparison.Ordinal);
            if (clientId is not null)
            {
                Assert.DoesNotContain(clientId, exception.Message, StringComparison.Ordinal);
            }

            if (clientSecret is not null)
            {
                Assert.DoesNotContain(clientSecret, exception.Message, StringComparison.Ordinal);
            }
        }
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> application) =>
        application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });

    private static async Task<CookieValue> CompleteLoginAsync(HttpClient client)
    {
        using var login = await client.GetAsync("/auth/discord/login");
        var location = Assert.IsType<Uri>(login.Headers.Location);
        var state = Assert.Single(QueryHelpers.ParseQuery(location.Query)["state"]);
        var correlation = GetCookie(login, "__Host-SquadUp.Correlation.");
        using var callbackRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/auth/discord/callback?code={Uri.EscapeDataString(AuthorizationCode)}&state={Uri.EscapeDataString(state!)}");
        callbackRequest.Headers.Add("Cookie", correlation.Pair);
        using var callback = await client.SendAsync(callbackRequest);
        var externalCookie = GetCookie(callback, "__Host-SquadUp.External");
        using var completionRequest = new HttpRequestMessage(
            HttpMethod.Get,
            DiscordOAuthDefaults.CompletionPath);
        completionRequest.Headers.Add("Cookie", externalCookie.Pair);
        using var completion = await client.SendAsync(completionRequest);

        Assert.Equal(HttpStatusCode.NoContent, completion.StatusCode);
        return GetCookie(completion, BrowserSessionExtensions.SessionCookieName);
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

    private sealed class DiscordApplication : WebApplicationFactory<Program>
    {
        private readonly DiscordBackchannelHandler backchannel;

        public DiscordApplication(DiscordBackchannelHandler backchannel)
        {
            this.backchannel = backchannel;
            ClientSecret = RandomNumberGenerator.GetHexString(32);
            using var rsa = RSA.Create(2048);
            PrivateKeyPem = rsa.ExportRSAPrivateKeyPem();
            ExternalLogins = new RecordingExternalLoginAccountService();
        }

        public string ClientSecret { get; }

        public string PrivateKeyPem { get; }

        public RecordingExternalLoginAccountService ExternalLogins { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:IdentityDatabase"] =
                        "Host=127.0.0.1;Port=1;Database=unavailable;Timeout=1",
                    ["ConnectionStrings:ProfileDatabase"] =
                        "Host=127.0.0.1;Port=1;Database=unavailable;Timeout=1",
                    ["Discord:ClientId"] = ClientId,
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
                    options => options.Backchannel = new HttpClient(backchannel, disposeHandler: false));
                services.RemoveAll<IExternalLoginAccountService>();
                services.AddSingleton<IExternalLoginAccountService>(ExternalLogins);
                services.RemoveAll<IUserSessionClaimsProvider>();
                services.AddSingleton<IUserSessionClaimsProvider>(
                    new StubUserSessionClaimsProvider(ExternalLogins.UserId));
            });
        }
    }

    private sealed class RecordingExternalLoginAccountService : IExternalLoginAccountService
    {
        public Guid UserId { get; } = Guid.CreateVersion7();

        public int UpsertCount { get; private set; }

        public string? LoginProvider { get; private set; }

        public string? ProviderKey { get; private set; }

        public Task<ExternalLoginUpsertResult> UpsertAsync(
            string loginProvider,
            string providerKey,
            CancellationToken cancellationToken)
        {
            UpsertCount++;
            LoginProvider = loginProvider;
            ProviderKey = providerKey;
            return Task.FromResult(new ExternalLoginUpsertResult(UserId, WasCreated: true));
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

    private sealed class StubUserSessionClaimsProvider(Guid userId) : IUserSessionClaimsProvider
    {
        public Task<UserSessionClaims?> FindAsync(
            Guid requestedUserId,
            CancellationToken cancellationToken) => Task.FromResult<UserSessionClaims?>(
                requestedUserId == userId
                    ? new UserSessionClaims(userId, [SquadUpRoles.Player])
                    : null);
    }

    public enum BackchannelFailure
    {
        None,
        TokenExchange,
        UserInformation
    }

    private sealed class DiscordBackchannelHandler : HttpMessageHandler
    {
        private readonly BackchannelFailure failure;
        private readonly string userId;

        public DiscordBackchannelHandler(
            BackchannelFailure failure = BackchannelFailure.None,
            string userId = DiscordUserId)
        {
            this.failure = failure;
            this.userId = userId;
            AccessToken = RandomNumberGenerator.GetHexString(32);
        }

        public string AccessToken { get; }

        public int TokenRequestCount { get; private set; }

        public int UserInformationRequestCount { get; private set; }

        public int RequestCount => TokenRequestCount + UserInformationRequestCount;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri == new Uri(DiscordOAuthDefaults.TokenEndpoint))
            {
                TokenRequestCount++;
                Assert.Equal(HttpMethod.Post, request.Method);
                var form = QueryHelpers.ParseQuery(
                    await request.Content!.ReadAsStringAsync(cancellationToken));
                Assert.Equal(AuthorizationCode, Assert.Single(form["code"]));
                Assert.Equal(
                    "https://localhost/auth/discord/callback",
                    Assert.Single(form["redirect_uri"]));

                if (failure == BackchannelFailure.TokenExchange)
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                return JsonResponse(
                    $$"""{"access_token":"{{AccessToken}}","token_type":"Bearer","expires_in":3600,"scope":"identify"}""");
            }

            if (request.RequestUri == new Uri(DiscordOAuthDefaults.UserInformationEndpoint))
            {
                UserInformationRequestCount++;
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                Assert.Equal(AccessToken, request.Headers.Authorization?.Parameter);

                if (failure == BackchannelFailure.UserInformation)
                {
                    return new HttpResponseMessage(HttpStatusCode.BadGateway);
                }

                return JsonResponse(
                    $$"""{"id":"{{userId}}","username":"synthetic-user","role":"Admin"}""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }
}
