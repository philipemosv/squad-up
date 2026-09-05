using System.Net.Http.Headers;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Retry;
using Polly.Timeout;
using SquadUp.Identity.Application;

namespace SquadUp.Api;

/// <summary>
/// Sends fixed API-to-Lobby requests using a newly minted, audience-bound delegated token.
/// The calling API boundary owns actor derivation; this client never accepts browser or provider credentials.
/// </summary>
internal interface ILobbyClient
{
    public Task<HttpResponseMessage> SendAsync(
        LobbyServiceRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// A server-created request for the Lobby HTTP boundary. The path is always relative to the configured Lobby base address.
/// </summary>
internal sealed record LobbyServiceRequest(
    HttpMethod Method,
    string RelativePath,
    Guid DelegatedUserId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Scopes,
    HttpContent? Content = null,
    string? IdempotencyKey = null);

internal sealed class LobbyClient(
    IHttpClientFactory httpClientFactory,
    IInternalAccessTokenIssuer tokenIssuer) : ILobbyClient
{
    public async Task<HttpResponseMessage> SendAsync(
        LobbyServiceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Method);
        ArgumentNullException.ThrowIfNull(request.Roles);
        ArgumentNullException.ThrowIfNull(request.Scopes);

        if (request.DelegatedUserId == Guid.Empty)
        {
            throw new ArgumentException("A Lobby request requires a delegated user.", nameof(request));
        }

        if (!Uri.TryCreate(request.RelativePath, UriKind.Relative, out var relativePath) ||
            request.RelativePath.Length == 0 || request.RelativePath[0] != '/' ||
            request.RelativePath.StartsWith("//", StringComparison.Ordinal))
        {
            throw new ArgumentException("The Lobby request path must be an absolute-path reference.", nameof(request));
        }

        using var message = new HttpRequestMessage(request.Method, relativePath)
        {
            Content = request.Content
        };
        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokenIssuer.IssueLobbyDelegatedToken(
                request.DelegatedUserId,
                request.Roles,
                request.Scopes));
        if (request.IdempotencyKey is not null)
        {
            message.Headers.Add("Idempotency-Key", request.IdempotencyKey);
        }

        var clientName = request.Method == HttpMethod.Get
            ? LobbyClientExtensions.ReadClientName
            : LobbyClientExtensions.CommandClientName;
        return await httpClientFactory.CreateClient(clientName).SendAsync(message, cancellationToken);
    }
}

internal static class LobbyClientExtensions
{
    internal const string ReadClientName = "SquadUp.Lobby.Read";
    internal const string CommandClientName = "SquadUp.Lobby.Command";

    public static IServiceCollection AddLobbyClient(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<LobbyClientOptions>()
            .Bind(configuration.GetSection(LobbyClientOptions.SectionName))
            .Validate(LobbyClientOptions.IsValid, LobbyClientOptions.ValidationError)
            .ValidateOnStart();
        services.AddHttpClient(ReadClientName, ConfigureLobbyClient)
            .AddResilienceHandler("lobby", static pipeline =>
            {
                pipeline.AddConcurrencyLimiter(permitLimit: 20, queueLimit: 0);
                pipeline.AddTimeout(new TimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(3) });
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 1,
                    Delay = TimeSpan.FromMilliseconds(200),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                });
                pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(20),
                    MinimumThroughput = 20,
                    BreakDuration = TimeSpan.FromSeconds(30)
                });
                pipeline.AddTimeout(new TimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(1) });
            });
        services.AddHttpClient(CommandClientName, ConfigureLobbyClient)
            .AddResilienceHandler("lobby", static pipeline =>
            {
                pipeline.AddConcurrencyLimiter(permitLimit: 20, queueLimit: 0);
                pipeline.AddTimeout(new TimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(3) });
                pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(20),
                    MinimumThroughput = 20,
                    BreakDuration = TimeSpan.FromSeconds(30)
                });
                pipeline.AddTimeout(new TimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(2) });
            });
        services.AddTransient<ILobbyClient, LobbyClient>();

        return services;
    }

    private static void ConfigureLobbyClient(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<IOptions<LobbyClientOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseAddress, UriKind.Absolute);
    }
}
