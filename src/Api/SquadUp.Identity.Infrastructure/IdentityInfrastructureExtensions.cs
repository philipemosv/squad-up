using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;

namespace SquadUp.Identity.Infrastructure;

public static class IdentityInfrastructureExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<IdentityPersistenceOptions>()
            .Configure(options => options.ConnectionString =
                configuration.GetConnectionString(IdentityPersistenceOptions.ConnectionStringName) ?? string.Empty)
            .Validate(static options => IsValidConnectionString(options.ConnectionString),
                IdentityPersistenceOptions.ValidationError)
            .ValidateOnStart();

        services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
        {
            var persistence = serviceProvider
                .GetRequiredService<IOptions<IdentityPersistenceOptions>>()
                .Value;

            options.UseNpgsql(persistence.ConnectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName)
                .MigrationsHistoryTable(IdentityDbContext.MigrationsHistoryTable, IdentityDbContext.SchemaName));
        });

        services
            .AddHealthChecks()
            .AddDbContextCheck<IdentityDbContext>(
                "identity_database",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        services
            .AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = false)
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<IdentityDbContext>();

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
