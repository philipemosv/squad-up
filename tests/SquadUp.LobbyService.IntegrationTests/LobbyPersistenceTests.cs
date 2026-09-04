using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using SquadUp.LobbyService.Domain;
using SquadUp.LobbyService.Infrastructure;

namespace SquadUp.LobbyService.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class LobbyPersistenceTests : IClassFixture<LobbyDatabaseFixture>
{
    private static readonly string[] ExpectedTables =
    [
        "game_catalog",
        "lobbies",
        "lobby_members",
        "migration_history",
        "rank_tiers"
    ];

    private readonly LobbyDatabaseFixture fixture;

    public LobbyPersistenceTests(LobbyDatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task InitialMigrationCreatesOnlyLobbyOwnedSchemaAndSeedsCatalog()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LobbyDbContext>();

        await context.Database.MigrateAsync(timeout.Token);

        var tables = await context.Database.SqlQueryRaw<string>(
            "SELECT table_name AS \"Value\" FROM information_schema.tables " +
            "WHERE table_schema = 'lobby' ORDER BY table_name").ToArrayAsync(timeout.Token);
        var unexpectedPublicTables = await context.Database.SqlQueryRaw<string>(
            "SELECT table_name AS \"Value\" FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY table_name").ToArrayAsync(timeout.Token);
        var catalogCount = await context.Database.SqlQueryRaw<int>(
            "SELECT count(*)::integer AS \"Value\" FROM lobby.rank_tiers WHERE game_id = 'dota2'")
            .SingleAsync(timeout.Token);

        Assert.Equal(ExpectedTables, tables);
        Assert.Empty(unexpectedPublicTables);
        Assert.Equal(8, catalogCount);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(timeout.Token));

        var health = await services.GetRequiredService<HealthCheckService>().CheckHealthAsync(
            registration => registration.Tags.Contains("ready"),
            timeout.Token);
        Assert.Equal(HealthStatus.Healthy, health.Status);
        Assert.Equal(HealthStatus.Healthy, health.Entries["lobby_database"].Status);
    }

    [Fact]
    public async Task MappingPersistsMembersAndRejectsDuplicateMembershipAndInvalidMemberCount()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var lobbyId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        await using var services = CreateServices();
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<LobbyDbContext>();
            await context.Database.MigrateAsync(timeout.Token);
            var lobby = new Lobby(lobbyId, Guid.NewGuid(), 2, new RankRequirement("dota2", 1, 8));
            lobby.AddMember(new LobbyMember(playerId, "123456789", "Player One", new PlayerRank("dota2", 4)));
            context.Lobbies.Add(lobby);
            await context.SaveChangesAsync(timeout.Token);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<LobbyDbContext>();
            var reloaded = await context.Lobbies
                .Include("members")
                .SingleAsync(candidate => candidate.Id == lobbyId, timeout.Token);

            Assert.Equal(1, reloaded.MembersCount);
            var member = Assert.Single(reloaded.Members);
            Assert.Equal(playerId, member.PlayerId);
            Assert.Equal("dota2", member.Rank.GameId);
            Assert.Equal(4, member.Rank.Ordinal);
        }

        await using var connection = new NpgsqlConnection(fixture.PostgreSql.GetConnectionString());
        await connection.OpenAsync(timeout.Token);
        await using (var duplicateMembership = connection.CreateCommand())
        {
            duplicateMembership.CommandText = """
                INSERT INTO lobby.lobby_members
                    (lobby_id, player_id, discord_user_id, display_name, rank_game_id, rank_ordinal)
                VALUES (@lobbyId, @playerId, '987654321', 'Duplicate', 'dota2', 4);
                """;
            duplicateMembership.Parameters.AddWithValue("lobbyId", lobbyId);
            duplicateMembership.Parameters.AddWithValue("playerId", playerId);

            var duplicate = await Assert.ThrowsAsync<PostgresException>(
                () => duplicateMembership.ExecuteNonQueryAsync(timeout.Token));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        }

        await using (var invalidMemberCount = connection.CreateCommand())
        {
            invalidMemberCount.CommandText =
                "UPDATE lobby.lobbies SET members_count = capacity + 1 WHERE id = @lobbyId";
            invalidMemberCount.Parameters.AddWithValue("lobbyId", lobbyId);

            var invalidCount = await Assert.ThrowsAsync<PostgresException>(
                () => invalidMemberCount.ExecuteNonQueryAsync(timeout.Token));
            Assert.Equal(PostgresErrorCodes.CheckViolation, invalidCount.SqlState);
        }
    }

    [Fact]
    public async Task GeneratedIdempotentSqlCanBeAppliedRepeatedly()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var scriptPath = Path.Combine(
            FindRepositoryRoot(),
            "docs/database/migrations/lobby/001_initial_lobby.sql");
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

    [Fact]
    public async Task XminRejectsAStaleLobbyTransition()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var lobbyId = Guid.NewGuid();
        await using var services = CreateServices();
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<LobbyDbContext>();
            await context.Database.MigrateAsync(timeout.Token);
            context.Lobbies.Add(new Lobby(lobbyId, Guid.NewGuid(), 2, new RankRequirement("dota2", 1)));
            await context.SaveChangesAsync(timeout.Token);
        }

        await using var firstScope = services.CreateAsyncScope();
        await using var secondScope = services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<LobbyDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<LobbyDbContext>();
        var firstLobby = await first.Lobbies.SingleAsync(candidate => candidate.Id == lobbyId, timeout.Token);
        var staleLobby = await second.Lobbies.SingleAsync(candidate => candidate.Id == lobbyId, timeout.Token);

        firstLobby.Cancel();
        await first.SaveChangesAsync(timeout.Token);

        staleLobby.Cancel();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(timeout.Token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-connection-string")]
    public async Task InvalidLobbyConnectionStringStopsStartupWithoutEchoingValue(string? invalidConnectionString)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:LobbyDatabase"] = invalidConnectionString
        });
        builder.Services.AddLobbyPersistence(builder.Configuration);
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(timeout.Token));

        Assert.Contains("ConnectionStrings:LobbyDatabase", exception.Message, StringComparison.Ordinal);
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
            ["ConnectionStrings:LobbyDatabase"] =
                "Host=127.0.0.1;Port=1;Database=unavailable;Timeout=1"
        });
        builder.Services.AddLobbyPersistence(builder.Configuration);
        using var host = builder.Build();

        await host.StartAsync(timeout.Token);
        await host.StopAsync(timeout.Token);
    }

    private ServiceProvider CreateServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LobbyDatabase"] = fixture.PostgreSql.GetConnectionString()
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLobbyPersistence(configuration);

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
