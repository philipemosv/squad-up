using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using SquadUp.Identity.Infrastructure;

namespace SquadUp.Api.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class IdentityPersistenceTests : IClassFixture<IdentityDatabaseFixture>
{
    private static readonly string[] ExpectedTables =
    [
        "migration_history",
        "role_claims",
        "roles",
        "user_claims",
        "user_logins",
        "user_roles",
        "user_tokens",
        "users"
    ];

    private readonly IdentityDatabaseFixture fixture;

    public IdentityPersistenceTests(IdentityDatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task InitialMigrationCreatesOnlyTheOwnedIdentitySchema()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        await context.Database.MigrateAsync(timeout.Token);

        var tables = await context.Database
            .SqlQueryRaw<string>(
                "SELECT table_name AS \"Value\" FROM information_schema.tables " +
                "WHERE table_schema = 'identity' ORDER BY table_name")
            .ToArrayAsync(timeout.Token);
        var unexpectedPublicTables = await context.Database
            .SqlQueryRaw<string>(
                "SELECT table_name AS \"Value\" FROM information_schema.tables " +
                "WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY table_name")
            .ToArrayAsync(timeout.Token);

        Assert.Equal(ExpectedTables, tables);
        Assert.Empty(unexpectedPublicTables);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(timeout.Token));

        var health = await services
            .GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(
                registration => registration.Tags.Contains("ready"),
                timeout.Token);
        Assert.Equal(HealthStatus.Healthy, health.Status);
        Assert.Equal(HealthStatus.Healthy, health.Entries["identity_database"].Status);
    }

    [Fact]
    public async Task IdentityStorePersistsLocalUserAndDiscordExternalLogin()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await using var migrationScope = services.CreateAsyncScope();
        await migrationScope.ServiceProvider
            .GetRequiredService<IdentityDbContext>()
            .Database
            .MigrateAsync(timeout.Token);

        await using var scope = services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new ApplicationUser { UserName = $"local-{suffix}" };

        var createResult = await userManager.CreateAsync(user);
        var loginResult = await userManager.AddLoginAsync(
            user,
            new UserLoginInfo("Discord", $"synthetic-{suffix}", "Discord"));
        var persisted = await userManager.FindByLoginAsync("Discord", $"synthetic-{suffix}");

        Assert.True(createResult.Succeeded, FormatErrors(createResult));
        Assert.True(loginResult.Succeeded, FormatErrors(loginResult));
        Assert.Equal(user.Id, persisted?.Id);
    }

    [Fact]
    public async Task GeneratedIdempotentSqlCanBeAppliedRepeatedly()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var repositoryRoot = FindRepositoryRoot();
        var scriptPath = Path.Combine(
            repositoryRoot,
            "docs/database/migrations/identity/001_initial_identity.sql");
        var sql = await File.ReadAllTextAsync(scriptPath, timeout.Token);
        await using var connection = new NpgsqlConnection(fixture.PostgreSql.GetConnectionString());
        await connection.OpenAsync(timeout.Token);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(timeout.Token);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-connection-string")]
    public async Task InvalidIdentityConnectionStringStopsStartupWithoutEchoingValue(
        string? invalidConnectionString)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:IdentityDatabase"] = invalidConnectionString
        });
        builder.Services.AddIdentityInfrastructure(builder.Configuration);
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(timeout.Token));

        Assert.Contains("ConnectionStrings:IdentityDatabase", exception.Message, StringComparison.Ordinal);
        if (invalidConnectionString is not null)
        {
            Assert.DoesNotContain(invalidConnectionString, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task HostStartupDoesNotConnectOrApplyMigrations()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:IdentityDatabase"] =
                "Host=127.0.0.1;Port=1;Database=unavailable;Timeout=1"
        });
        builder.Services.AddIdentityInfrastructure(builder.Configuration);
        using var host = builder.Build();

        await host.StartAsync(timeout.Token);
        await host.StopAsync(timeout.Token);
    }

    private ServiceProvider CreateServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDatabase"] = fixture.PostgreSql.GetConnectionString()
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityInfrastructure(configuration);

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static string FormatErrors(IdentityResult result) => string.Join(
        "; ",
        result.Errors.Select(error => error.Code));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SquadUp.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}
