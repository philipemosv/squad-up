using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;
using SquadUp.Profile.Application;

namespace SquadUp.Profile.Infrastructure;

public static class ProfileInfrastructureExtensions
{
    public static IServiceCollection AddProfileInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<ProfilePersistenceOptions>()
            .Configure(options => options.ConnectionString =
                configuration.GetConnectionString(ProfilePersistenceOptions.ConnectionStringName) ?? string.Empty)
            .Validate(static options => IsValidConnectionString(options.ConnectionString),
                ProfilePersistenceOptions.ValidationError)
            .ValidateOnStart();

        services.AddDbContext<ProfileDbContext>((serviceProvider, options) =>
        {
            var persistence = serviceProvider
                .GetRequiredService<IOptions<ProfilePersistenceOptions>>()
                .Value;

            options.UseNpgsql(persistence.ConnectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(ProfileDbContext).Assembly.FullName)
                .MigrationsHistoryTable(ProfileDbContext.MigrationsHistoryTable, ProfileDbContext.SchemaName));
        });

        services
            .AddHealthChecks()
            .AddDbContextCheck<ProfileDbContext>(
                "profile_database",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        services.AddScoped<IPlayerProfileService, PlayerProfileService>();
        services.AddScoped<IPlayerGameService, PlayerGameService>();
        services.AddScoped<IGameCatalogQueryService, GameCatalogQueryService>();

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
            return !string.IsNullOrWhiteSpace(builder.Host) &&
                !string.IsNullOrWhiteSpace(builder.Database);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
