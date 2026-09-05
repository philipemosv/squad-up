using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SquadUp.LobbyService.Application;
using SquadUp.LobbyService.Domain;
using StackExchange.Redis;

namespace SquadUp.LobbyService.Infrastructure;

public static class LobbyReadCachingExtensions
{
    public static IServiceCollection AddLobbyReadCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddStackExchangeRedisCache(
            options => options.Configuration = configuration.GetConnectionString("LobbyCache"));
        services.AddHybridCache();

        services.AddSingleton<ILobbyReadCache, LobbyReadCache>();
        services.AddSingleton<IRedisLeaseManager>(_ => new RedisLeaseManager(configuration));
        services.Replace(ServiceDescriptor.Singleton<ILobbySearchCacheInvalidator, LobbySearchCacheGeneration>());
        services.Replace(ServiceDescriptor.Scoped<ILobbyQueryService, CachedLobbyQueryService>());
        return services;
    }
}

internal sealed class LobbyReadCache(HybridCache cache) : ILobbyReadCache
{
    public async Task<TProjection> GetOrCreateAsync<TProjection>(
        string cacheKey,
        Func<CancellationToken, ValueTask<TProjection>> factory,
        LobbyReadCacheEntryOptions? options,
        CancellationToken cancellationToken)
        where TProjection : class, IAllowlistedLobbyReadProjection
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentNullException.ThrowIfNull(factory);

        TProjection? loadedProjection = null;
        Exception? loaderFailure = null;
        try
        {
            return await cache.GetOrCreateAsync(
                cacheKey,
                async token =>
                {
                    try
                    {
                        loadedProjection = await factory(token);
                        return loadedProjection;
                    }
                    catch (Exception exception)
                    {
                        loaderFailure = exception;
                        throw;
                    }
                },
                options is null ? null : new HybridCacheEntryOptions { Expiration = options.Expiration },
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch when (loaderFailure is not null)
        {
            throw;
        }
        catch
        {
            return loadedProjection ?? await factory(cancellationToken);
        }
    }
}

internal sealed class LobbySearchCacheGeneration : ILobbySearchCacheInvalidator
{
    private long generation;

    public long CurrentGeneration => Interlocked.Read(ref generation);

    public void Invalidate() => Interlocked.Increment(ref generation);
}

internal sealed class CachedLobbyQueryService(
    LobbyQueryService queries,
    ILobbyReadCache cache,
    ILobbySearchCacheInvalidator invalidator,
    IRedisLeaseManager leases) : ILobbyQueryService
{
    public async Task<LobbySearchPageDto> SearchRecruitingAsync(
        SearchRecruitingLobbiesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalized = LobbySearchCacheKey.Normalize(request);
        var generation = invalidator.CurrentGeneration;
        var cacheKey = LobbySearchCacheKey.Create(normalized, generation);
        var options = new LobbyReadCacheEntryOptions(LobbySearchCacheKey.CreateSearchExpiration());

        await using var lease = await leases.TryAcquireAsync(
            cacheKey,
            LobbySearchCacheKey.LeaseDuration,
            cancellationToken);
        if (lease is null)
        {
            await Task.Delay(LobbySearchCacheKey.CreateLeaseContentionDelay(), cancellationToken);
        }

        return await cache.GetOrCreateAsync(
            cacheKey,
            token => new ValueTask<LobbySearchPageDto>(queries.SearchRecruitingAsync(normalized, token)),
            options,
            cancellationToken);
    }
}

internal static class LobbySearchCacheKey
{
    private const int MinimumSearchTtlSeconds = 10;
    private const int MaximumSearchTtlSeconds = 20;
    private const int MinimumLeaseContentionDelayMilliseconds = 10;
    private const int MaximumLeaseContentionDelayMilliseconds = 25;

    public static TimeSpan LeaseDuration => TimeSpan.FromSeconds(2);

    public static SearchRecruitingLobbiesRequest Normalize(SearchRecruitingLobbiesRequest request)
    {
        var gameId = request.GameId?.Trim().ToLowerInvariant();
        if (gameId is { Length: 0 } or { Length: > RankRequirement.MaxGameIdLength })
        {
            throw new ArgumentException("Game id must be a bounded non-empty value when supplied.", nameof(request));
        }

        if (request.PageSize is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Page size must be between 1 and 50.");
        }

        return new SearchRecruitingLobbiesRequest(gameId, request.AfterLobbyId, request.PageSize);
    }

    public static string Create(SearchRecruitingLobbiesRequest request, long generation)
    {
        var canonical = $"game={request.GameId ?? "*"}&after={request.AfterLobbyId?.ToString("D") ?? "*"}&size={request.PageSize}&generation={generation}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return $"squadup:lobby:search:v1:{hash}";
    }

    public static TimeSpan CreateSearchExpiration() => TimeSpan.FromSeconds(Random.Shared.Next(
        MinimumSearchTtlSeconds,
        MaximumSearchTtlSeconds + 1));

    public static TimeSpan CreateLeaseContentionDelay() => TimeSpan.FromMilliseconds(Random.Shared.Next(
        MinimumLeaseContentionDelayMilliseconds,
        MaximumLeaseContentionDelayMilliseconds + 1));
}

/// <summary>
/// Coordinates a best-effort Redis lease for a server-generated cache key. It is
/// not a distributed transaction and never establishes business exclusivity.
/// </summary>
internal interface IRedisLeaseManager
{
    public Task<IAsyncDisposable?> TryAcquireAsync(
        string serverGeneratedCacheKey,
        TimeSpan duration,
        CancellationToken cancellationToken);
}

internal sealed class RedisLeaseManager(IConfiguration configuration) : IRedisLeaseManager, IDisposable
{
    private const string ReleaseScript = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) end return 0";
    private readonly object multiplexerLock = new();
    private ConnectionMultiplexer? multiplexer;
    private bool multiplexerInitialized;

    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string serverGeneratedCacheKey,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverGeneratedCacheKey);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var redis = GetMultiplexer();
        if (redis is null)
        {
            return null;
        }

        try
        {
            var database = redis.GetDatabase();
            var key = CreateLeaseKey(serverGeneratedCacheKey);
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var acquired = await database.StringSetAsync(key, token, duration, When.NotExists);
            return acquired ? new RedisLease(database, key, token) : null;
        }
        catch (RedisException)
        {
            return null;
        }
    }

    public void Dispose() => multiplexer?.Dispose();

    private ConnectionMultiplexer? GetMultiplexer()
    {
        lock (multiplexerLock)
        {
            if (multiplexerInitialized)
            {
                return multiplexer;
            }

            multiplexerInitialized = true;
            var connectionString = configuration.GetConnectionString("LobbyCache");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return null;
            }

            try
            {
                var options = ConfigurationOptions.Parse(connectionString);
                options.AbortOnConnectFail = false;
                options.ConnectRetry = 0;
                multiplexer = ConnectionMultiplexer.Connect(options);
                return multiplexer;
            }
            catch (RedisException)
            {
                return null;
            }
        }
    }

    private static RedisKey CreateLeaseKey(string serverGeneratedCacheKey)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serverGeneratedCacheKey))).ToLowerInvariant();
        return $"squadup:lobby:search:lease:v1:{hash}";
    }

    private sealed class RedisLease(IDatabase database, RedisKey key, RedisValue token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await database.ScriptEvaluateAsync(ReleaseScript, [key], [token]);
            }
            catch (RedisException)
            {
                // Lease release is best effort. Expiry is the safety net when Redis is unavailable.
            }
        }
    }
}
