using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Polly.CircuitBreaker;
using Polly.Timeout;
using SquadUp.Api;
using SquadUp.Identity.Application;
using SquadUp.Identity.Infrastructure;

namespace SquadUp.Api.IntegrationTests;

public sealed class LobbyClientTests
{
    [Fact]
    public async Task DelegatesOnlyTheLocalActorAndAllowlistedAuthorityToTheConfiguredLobbyOrigin()
    {
        using var key = RSA.Create(2048);
        var handler = new RecordingHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));
        await using var provider = CreateProvider(key.ExportRSAPrivateKeyPem(), handler);
        var client = provider.GetRequiredService<ILobbyClient>();
        var playerId = Guid.CreateVersion7();

        using var response = await client.SendAsync(new LobbyServiceRequest(
            HttpMethod.Post,
            "/lobbies/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/members",
            playerId,
            [SquadUpRoles.Player],
            ["lobby.write"],
            IdempotencyKey: "synthetic-idempotency-key"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://lobby.squad-up.test/lobbies/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/members", request.Uri);
        Assert.Equal("synthetic-idempotency-key", request.IdempotencyKey);
        Assert.NotNull(request.BearerToken);
        var token = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().ReadJsonWebToken(request.BearerToken);
        Assert.Equal(playerId.ToString("D"), token.Subject);
        Assert.Equal("delegated_user", token.GetClaim("token_kind").Value);
        Assert.Equal("lobby.write", token.GetClaim("scope").Value);
        Assert.Equal(SquadUpRoles.Player, token.GetClaim(SquadUpClaimTypes.Role).Value);
        Assert.DoesNotContain("discord", request.BearerToken!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefusesUntrustedPathsAndMissingDelegatedActorsBeforeAnyNetworkCall()
    {
        using var key = RSA.Create(2048);
        var handler = new RecordingHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));
        await using var provider = CreateProvider(key.ExportRSAPrivateKeyPem(), handler);
        var client = provider.GetRequiredService<ILobbyClient>();

        await Assert.ThrowsAsync<ArgumentException>(() => client.SendAsync(new LobbyServiceRequest(
            HttpMethod.Get,
            "https://attacker.invalid/lobbies",
            Guid.CreateVersion7(),
            [SquadUpRoles.Player],
            ["lobby.read"]),
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => client.SendAsync(new LobbyServiceRequest(
            HttpMethod.Get,
            "/lobbies",
            Guid.Empty,
            [SquadUpRoles.Player],
            ["lobby.read"]),
            CancellationToken.None));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task DoesNotRetryCommandsAndOpensTheCircuitAfterTheConfiguredFailureWindow()
    {
        using var key = RSA.Create(2048);
        var handler = new RecordingHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await using var provider = CreateProvider(key.ExportRSAPrivateKeyPem(), handler);
        var client = provider.GetRequiredService<ILobbyClient>();
        var request = new LobbyServiceRequest(
            HttpMethod.Post,
            "/lobbies",
            Guid.CreateVersion7(),
            [SquadUpRoles.Player],
            ["lobby.write"],
            IdempotencyKey: "synthetic-idempotency-key");

        for (var attempt = 0; attempt < 20; attempt++)
        {
            using var response = await client.SendAsync(request, CancellationToken.None);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        await Assert.ThrowsAsync<BrokenCircuitException>(() =>
            client.SendAsync(request, CancellationToken.None));
        Assert.Equal(20, handler.Requests.Length);
    }

    [Fact]
    public async Task RetriesOneTransientReadFailureWithBoundedJitter()
    {
        using var key = RSA.Create(2048);
        var attempt = 0;
        var handler = new RecordingHandler(_ => Task.FromResult(new HttpResponseMessage(
            Interlocked.Increment(ref attempt) == 1
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.OK)));
        await using var provider = CreateProvider(key.ExportRSAPrivateKeyPem(), handler);
        var client = provider.GetRequiredService<ILobbyClient>();

        using var response = await client.SendAsync(new LobbyServiceRequest(
            HttpMethod.Get,
            "/lobbies",
            Guid.CreateVersion7(),
            [SquadUpRoles.Player],
            ["lobby.read"]),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.Requests.Length);
    }

    [Fact]
    public async Task EnforcesTheTwoSecondCommandAttemptTimeoutWithoutRetry()
    {
        using var key = RSA.Create(2048);
        var handler = new RecordingHandler(async cancellationToken =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await using var provider = CreateProvider(key.ExportRSAPrivateKeyPem(), handler);
        var client = provider.GetRequiredService<ILobbyClient>();

        await Assert.ThrowsAsync<TimeoutRejectedException>(() => client.SendAsync(new LobbyServiceRequest(
            HttpMethod.Post,
            "/lobbies",
            Guid.CreateVersion7(),
            [SquadUpRoles.Player],
            ["lobby.write"],
            IdempotencyKey: "synthetic-idempotency-key"),
            CancellationToken.None));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ReturnsSanitized503ForAnUnavailableReadDependency()
    {
        var result = await LobbyGatewayEndpoints.SendAsync(
            new DefaultHttpContext(),
            new ThrowingLobbyClient(new HttpRequestException("synthetic connection failure")),
            new LobbyServiceRequest(
                HttpMethod.Get,
                "/lobbies",
                Guid.CreateVersion7(),
                [SquadUpRoles.Player],
                ["lobby.read"]));

        var response = await ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.StatusCode);
        Assert.Contains("lobby_temporarily_unavailable", response.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic connection failure", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DoesNotReportAnUnavailableCommandAsCompleted()
    {
        var result = await LobbyGatewayEndpoints.SendAsync(
            new DefaultHttpContext(),
            new ThrowingLobbyClient(new HttpRequestException("synthetic connection failure")),
            new LobbyServiceRequest(
                HttpMethod.Post,
                "/lobbies/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/cancel",
                Guid.CreateVersion7(),
                [SquadUpRoles.Player],
                ["lobby.write"]));

        var response = await ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status204NoContent, response.StatusCode);
        Assert.Contains("lobby_temporarily_unavailable", response.Body, StringComparison.Ordinal);
    }

    private static async Task<(int StatusCode, string Body)> ExecuteAsync(IResult result)
    {
        var context = new DefaultHttpContext();
        using var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        context.RequestServices = services;
        await using var body = new MemoryStream();
        context.Response.Body = body;
        await result.ExecuteAsync(context);
        return (context.Response.StatusCode, Encoding.UTF8.GetString(body.ToArray()));
    }

    private static ServiceProvider CreateProvider(string privateKeyPem, RecordingHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalTokens:Issuer"] = "https://api.squad-up.test",
                ["InternalTokens:LobbyAudience"] = "squad-up-lobby",
                ["InternalTokens:ClientId"] = "squad-up-api",
                ["InternalTokens:LifetimeSeconds"] = "120",
                ["InternalTokens:ActiveKeyId"] = "test-current",
                ["InternalTokens:PrivateKeyPem"] = privateKeyPem,
                ["InternalTokens:AllowedScopes:0"] = "lobby.read",
                ["InternalTokens:AllowedScopes:1"] = "lobby.write",
                ["LobbyClient:BaseAddress"] = "https://lobby.squad-up.test"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInternalTokenIssuer(configuration);
        services.AddLobbyClient(configuration);
        services.Configure<HttpClientFactoryOptions>(LobbyClientExtensions.ReadClientName, options =>
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = handler));
        services.Configure<HttpClientFactoryOptions>(LobbyClientExtensions.CommandClientName, options =>
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = handler));
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class RecordingHandler(Func<CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        private readonly object sync = new();
        private readonly List<RecordedRequest> requests = [];

        public RecordedRequest[] Requests
        {
            get
            {
                lock (sync)
                {
                    return requests.ToArray();
                }
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            lock (sync)
            {
                requests.Add(new RecordedRequest(
                    request.RequestUri?.ToString(),
                    request.Headers.Authorization?.Parameter,
                    request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null));
            }

            return response(cancellationToken);
        }
    }

    private sealed class ThrowingLobbyClient(Exception exception) : ILobbyClient
    {
        public Task<HttpResponseMessage> SendAsync(
            LobbyServiceRequest request,
            CancellationToken cancellationToken) => Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed record RecordedRequest(string? Uri, string? BearerToken, string? IdempotencyKey);
}
