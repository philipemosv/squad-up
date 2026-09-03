using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using SquadUp.Profile.Infrastructure;

namespace SquadUp.Api.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class ProfilePersistenceTests : IClassFixture<ProfileDatabaseFixture>
{
    private static readonly string[] ExpectedTables =
    [
        "games",
        "migration_history",
        "player_games",
        "player_profiles",
        "rank_tiers"
    ];

    private readonly ProfileDatabaseFixture fixture;

    public ProfilePersistenceTests(ProfileDatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task InitialMigrationCreatesOnlyTheOwnedProfileSchema()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ProfileDbContext>();

        await context.Database.MigrateAsync(timeout.Token);

        var tables = await context.Database
            .SqlQueryRaw<string>(
                "SELECT table_name AS \"Value\" FROM information_schema.tables " +
                "WHERE table_schema = 'profile' ORDER BY table_name")
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
        Assert.Equal(HealthStatus.Healthy, health.Entries["profile_database"].Status);
    }

    [Fact]
    public async Task MigrationSeedsDota2GameWithEightOrderedRankTiers()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ProfileDbContext>();
        await context.Database.MigrateAsync(timeout.Token);

        var game = await context.Games.SingleAsync(candidate => candidate.Id == "dota2", timeout.Token);
        var tiers = await context.RankTiers
            .Where(tier => tier.GameId == "dota2")
            .OrderBy(tier => tier.Ordinal)
            .Select(tier => new { tier.TierId, tier.Ordinal })
            .ToArrayAsync(timeout.Token);

        Assert.Equal("Dota 2", game.Name);
        Assert.True(game.IsActive);
        Assert.Equal(8, tiers.Length);
        Assert.Equal("herald", tiers[0].TierId);
        Assert.Equal(1, tiers[0].Ordinal);
        Assert.Equal("immortal", tiers[^1].TierId);
        Assert.Equal(8, tiers[^1].Ordinal);
        Assert.Equal(Enumerable.Range(1, 8), tiers.Select(tier => tier.Ordinal));
    }

    [Theory]
    [InlineData("001_initial_profile.sql")]
    [InlineData("002_seed_dota2_catalog.sql")]
    public async Task GeneratedIdempotentSqlCanBeAppliedRepeatedly(string scriptName)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var repositoryRoot = FindRepositoryRoot();
        var scriptPath = Path.Combine(
            repositoryRoot,
            "docs/database/migrations/profile",
            scriptName);
        var sql = await File.ReadAllTextAsync(scriptPath, timeout.Token);
        await using var connection = new NpgsqlConnection(fixture.PostgreSql.GetConnectionString());
        await connection.OpenAsync(timeout.Token);

        if (scriptName != "001_initial_profile.sql")
        {
            var baselineSql = await File.ReadAllTextAsync(
                Path.Combine(
                    repositoryRoot,
                    "docs/database/migrations/profile/001_initial_profile.sql"),
                timeout.Token);
            await using var baselineCommand = connection.CreateCommand();
            baselineCommand.CommandText = baselineSql;
            await baselineCommand.ExecuteNonQueryAsync(timeout.Token);
        }

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
    public async Task InvalidProfileConnectionStringStopsStartupWithoutEchoingValue(
        string? invalidConnectionString)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ProfileDatabase"] = invalidConnectionString
        });
        builder.Services.AddProfileInfrastructure(builder.Configuration);
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(timeout.Token));

        Assert.Contains("ConnectionStrings:ProfileDatabase", exception.Message, StringComparison.Ordinal);
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
            ["ConnectionStrings:ProfileDatabase"] =
                "Host=127.0.0.1;Port=1;Database=unavailable;Timeout=1"
        });
        builder.Services.AddProfileInfrastructure(builder.Configuration);
        using var host = builder.Build();

        await host.StartAsync(timeout.Token);
        await host.StopAsync(timeout.Token);
    }

    private ServiceProvider CreateServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ProfileDatabase"] = fixture.PostgreSql.GetConnectionString()
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProfileInfrastructure(configuration);

        return services.BuildServiceProvider(validateScopes: true);
    }

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
