using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadUp.LobbyService.Application;

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
        return services;
    }
}

internal sealed class LobbyReadCache(HybridCache cache) : ILobbyReadCache
{
    public async Task<TProjection> GetOrCreateAsync<TProjection>(
        string cacheKey,
        Func<CancellationToken, ValueTask<TProjection>> factory,
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
