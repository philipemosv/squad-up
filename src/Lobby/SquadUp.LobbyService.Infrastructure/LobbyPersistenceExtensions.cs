using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;
using SquadUp.LobbyService.Application;

namespace SquadUp.LobbyService.Infrastructure;

public static class LobbyPersistenceExtensions
{
    public static IServiceCollection AddLobbyPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<LobbyPersistenceOptions>()
            .Configure(options => options.ConnectionString =
                configuration.GetConnectionString(LobbyPersistenceOptions.ConnectionStringName) ?? string.Empty)
            .Validate(static options => IsValidConnectionString(options.ConnectionString), LobbyPersistenceOptions.ValidationError)
            .ValidateOnStart();

        services.AddDbContext<LobbyDbContext>((serviceProvider, options) =>
        {
            var persistence = serviceProvider.GetRequiredService<IOptions<LobbyPersistenceOptions>>().Value;
            options.UseNpgsql(persistence.ConnectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(LobbyDbContext).Assembly.FullName)
                .MigrationsHistoryTable(LobbyDbContext.MigrationsHistoryTable, LobbyDbContext.SchemaName));
        });

        services
            .AddHealthChecks()
            .AddDbContextCheck<LobbyDbContext>(
                "lobby_database",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        services.AddScoped<ILobbyCommandService, LobbyCommandService>();
        services.AddScoped<ILobbyQueryService, LobbyQueryService>();

        return services;
    }

    private static bool IsValidConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return !string.IsNullOrWhiteSpace(builder.Host) && !string.IsNullOrWhiteSpace(builder.Database);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
