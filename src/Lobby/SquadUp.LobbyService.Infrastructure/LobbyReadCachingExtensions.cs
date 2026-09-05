using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SquadUp.LobbyService.Application;
using SquadUp.LobbyService.Domain;

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
    ILobbySearchCacheInvalidator invalidator) : ILobbyQueryService
{
    public Task<LobbySearchPageDto> SearchRecruitingAsync(
        SearchRecruitingLobbiesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalized = LobbySearchCacheKey.Normalize(request);
        var generation = invalidator.CurrentGeneration;
        var cacheKey = LobbySearchCacheKey.Create(normalized, generation);
        var options = new LobbyReadCacheEntryOptions(LobbySearchCacheKey.CreateSearchExpiration());

        return cache.GetOrCreateAsync(
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
}
