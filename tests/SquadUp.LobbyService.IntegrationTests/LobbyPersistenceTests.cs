using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using SquadUp.LobbyService.Application;
using SquadUp.LobbyService.Domain;
using SquadUp.LobbyService.Infrastructure;

namespace SquadUp.LobbyService.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class LobbyPersistenceTests : IClassFixture<LobbyDatabaseFixture>
{
    private static readonly string[] ExpectedTables =
    [
        "game_catalog",
        "http_idempotency_keys",
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
        await using var connection = new NpgsqlConnection(fixture.PostgreSql.GetConnectionString());
        await connection.OpenAsync(timeout.Token);

        foreach (var migration in new[] { "001_initial_lobby.sql", "002_http_idempotency_ledger.sql" })
        {
            var scriptPath = Path.Combine(FindRepositoryRoot(), "docs/database/migrations/lobby", migration);
            var sql = await File.ReadAllTextAsync(scriptPath, timeout.Token);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync(timeout.Token);
            }
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

    [Fact]
    public async Task CreateCommandPersistsARecruitingLobbyOnlyForActiveCatalogValues()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LobbyDbContext>();
        var commands = scope.ServiceProvider.GetRequiredService<ILobbyCommandService>();
        await context.Database.MigrateAsync(timeout.Token);

        var created = await commands.CreateAsync(
            Guid.NewGuid(),
            new CreateLobbyRequest(5, " DOTA2 ", 2, 5),
            timeout.Token);
        var unknownGame = await commands.CreateAsync(
            Guid.NewGuid(),
            new CreateLobbyRequest(5, "unknown", 1, null),
            timeout.Token);
        var unknownRank = await commands.CreateAsync(
            Guid.NewGuid(),
            new CreateLobbyRequest(5, "dota2", 9, null),
            timeout.Token);
        var invalidCapacity = await commands.CreateAsync(
            Guid.NewGuid(),
            new CreateLobbyRequest(1, "dota2", 1, null),
            timeout.Token);

        Assert.Equal(CreateLobbyOutcome.Success, created.Outcome);
        Assert.NotNull(created.LobbyId);
        Assert.Equal(CreateLobbyOutcome.GameNotFound, unknownGame.Outcome);
        Assert.Equal(CreateLobbyOutcome.RankTierNotFound, unknownRank.Outcome);
        Assert.Equal(CreateLobbyOutcome.ValidationFailed, invalidCapacity.Outcome);

        var lobby = await context.Lobbies.SingleAsync(candidate => candidate.Id == created.LobbyId, timeout.Token);
        Assert.Equal(LobbyStatus.Recruiting, lobby.Status);
        Assert.Equal("dota2", lobby.RankRequirement.GameId);
        Assert.Equal(2, lobby.RankRequirement.MinimumOrdinal);
        Assert.Equal(5, lobby.RankRequirement.MaximumOrdinal);
    }

    [Fact]
    public async Task SearchQueryProjectsOnlyRecruitingLobbySummariesWithoutTrackingEntities()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LobbyDbContext>();
        var commands = scope.ServiceProvider.GetRequiredService<ILobbyCommandService>();
        var queries = scope.ServiceProvider.GetRequiredService<ILobbyQueryService>();
        await context.Database.MigrateAsync(timeout.Token);

        var recruiting = await commands.CreateAsync(
            Guid.NewGuid(),
            new CreateLobbyRequest(5, "dota2", 1, null),
            timeout.Token);
        var cancelled = new Lobby(Guid.NewGuid(), Guid.NewGuid(), 5, new RankRequirement("dota2", 1));
        cancelled.Cancel();
        context.Lobbies.Add(cancelled);
        await context.SaveChangesAsync(timeout.Token);
        context.ChangeTracker.Clear();

        var page = await queries.SearchRecruitingAsync(
            new SearchRecruitingLobbiesRequest("dota2", null, 20),
            timeout.Token);
        var summaries = page.Items;

        var summary = Assert.Single(summaries, summary => summary.LobbyId == recruiting.LobbyId);
        Assert.Equal(5, summary.Capacity);
        Assert.Equal(0, summary.MembersCount);
        Assert.Equal("dota2", summary.GameId);
        Assert.DoesNotContain(summaries, summary => summary.LobbyId == cancelled.Id);
        Assert.Null(page.NextCursor);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task SearchQueryUsesKeysetPaginationAndReturnsOnlyTheNextPageCursor()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LobbyDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<ILobbyQueryService>();
        await context.Database.MigrateAsync(timeout.Token);

        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var thirdId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        context.Lobbies.AddRange(
            new Lobby(firstId, Guid.NewGuid(), 5, new RankRequirement("dota2", 1)),
            new Lobby(secondId, Guid.NewGuid(), 5, new RankRequirement("dota2", 1)),
            new Lobby(thirdId, Guid.NewGuid(), 5, new RankRequirement("dota2", 1)));
        await context.SaveChangesAsync(timeout.Token);
        context.ChangeTracker.Clear();

        var firstPage = await queries.SearchRecruitingAsync(
            new SearchRecruitingLobbiesRequest(" DOTA2 ", null, 2),
            timeout.Token);
        Assert.Equal([firstId, secondId], firstPage.Items.Select(summary => summary.LobbyId));
        var cursor = Assert.IsType<string>(firstPage.NextCursor);
        Assert.True(LobbySearchCursor.TryDecode(cursor, out var afterLobbyId));
        Assert.Equal(secondId, afterLobbyId);

        var secondPage = await queries.SearchRecruitingAsync(
            new SearchRecruitingLobbiesRequest("dota2", afterLobbyId, 2),
            timeout.Token);
        Assert.Equal([thirdId], secondPage.Items.Select(summary => summary.LobbyId));
        Assert.Null(secondPage.NextCursor);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task JoinAndLeaveCommandsPersistOnlyValidRecruitingMembershipChanges()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LobbyDbContext>();
        var commands = scope.ServiceProvider.GetRequiredService<ILobbyCommandService>();
        await context.Database.MigrateAsync(timeout.Token);

        var created = await commands.CreateAsync(
            Guid.NewGuid(),
            new CreateLobbyRequest(3, "dota2", 2, 5),
            timeout.Token);
        var lobbyId = Assert.IsType<Guid>(created.LobbyId);
        var playerId = Guid.NewGuid();
        var request = new JoinLobbyRequest("123456789", "Synthetic Player", "dota2", 3);

        var joined = await commands.JoinAsync(lobbyId, playerId, request, timeout.Token);
        var duplicate = await commands.JoinAsync(lobbyId, playerId, request, timeout.Token);
        var insufficientRank = await commands.JoinAsync(
            lobbyId,
            Guid.NewGuid(),
            request with { RankOrdinal = 1 },
            timeout.Token);
        var invalidPlayer = await commands.JoinAsync(lobbyId, Guid.Empty, request, timeout.Token);
        var missingLobby = await commands.JoinAsync(Guid.NewGuid(), Guid.NewGuid(), request, timeout.Token);
        var unknownMember = await commands.LeaveAsync(lobbyId, Guid.NewGuid(), timeout.Token);
        var left = await commands.LeaveAsync(lobbyId, playerId, timeout.Token);

        Assert.Equal(LobbyMembershipOutcome.Success, joined.Outcome);
        Assert.Equal(LobbyMembershipOutcome.Rejected, duplicate.Outcome);
        Assert.Equal(LobbyMembershipOutcome.Rejected, insufficientRank.Outcome);
        Assert.Equal(LobbyMembershipOutcome.ValidationFailed, invalidPlayer.Outcome);
        Assert.Equal(LobbyMembershipOutcome.LobbyNotFound, missingLobby.Outcome);
        Assert.Equal(LobbyMembershipOutcome.Rejected, unknownMember.Outcome);
        Assert.Equal(LobbyMembershipOutcome.Success, left.Outcome);

        context.ChangeTracker.Clear();
        var lobby = await context.Lobbies
            .Include("members")
            .SingleAsync(candidate => candidate.Id == lobbyId, timeout.Token);
        Assert.Equal(LobbyStatus.Recruiting, lobby.Status);
        Assert.Equal(0, lobby.MembersCount);
        Assert.Empty(lobby.Members);
    }

    [Fact]
    public async Task CancelCommandAuthorizesCurrentOwnerOrModeratorBeforePersistingTheTransition()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LobbyDbContext>();
        var commands = scope.ServiceProvider.GetRequiredService<ILobbyCommandService>();
        await context.Database.MigrateAsync(timeout.Token);

        var ownerId = Guid.NewGuid();
        var differentPlayerId = Guid.NewGuid();
        var ownerLobby = new Lobby(Guid.NewGuid(), ownerId, 2, new RankRequirement("dota2", 1));
        var moderatorLobby = new Lobby(Guid.NewGuid(), ownerId, 2, new RankRequirement("dota2", 1));
        var terminalLobby = new Lobby(Guid.NewGuid(), ownerId, 2, new RankRequirement("dota2", 1));
        terminalLobby.Cancel();
        context.Lobbies.AddRange(ownerLobby, moderatorLobby, terminalLobby);
        await context.SaveChangesAsync(timeout.Token);

        var owner = await commands.CancelAsync(ownerLobby.Id, CreateActor(ownerId, "Player"), timeout.Token);
        var differentPlayer = await commands.CancelAsync(
            moderatorLobby.Id,
            CreateActor(differentPlayerId, "Player"),
            timeout.Token);
        var moderator = await commands.CancelAsync(
            moderatorLobby.Id,
            CreateActor(differentPlayerId, "Moderator"),
            timeout.Token);
        var missing = await commands.CancelAsync(Guid.NewGuid(), CreateActor(ownerId, "Player"), timeout.Token);
        var invalid = await commands.CancelAsync(Guid.Empty, CreateActor(ownerId, "Player"), timeout.Token);
        var terminal = await commands.CancelAsync(terminalLobby.Id, CreateActor(ownerId, "Player"), timeout.Token);

        Assert.Equal(LobbyCancellationOutcome.Success, owner.Outcome);
        Assert.Equal(LobbyCancellationOutcome.Forbidden, differentPlayer.Outcome);
        Assert.Equal(LobbyCancellationOutcome.Success, moderator.Outcome);
        Assert.Equal(LobbyCancellationOutcome.LobbyNotFound, missing.Outcome);
        Assert.Equal(LobbyCancellationOutcome.ValidationFailed, invalid.Outcome);
        Assert.Equal(LobbyCancellationOutcome.Rejected, terminal.Outcome);

        context.ChangeTracker.Clear();
        var lobbies = await context.Lobbies.ToDictionaryAsync(lobby => lobby.Id, timeout.Token);
        Assert.Equal(LobbyStatus.Cancelled, lobbies[ownerLobby.Id].Status);
        Assert.Equal(LobbyStatus.Cancelled, lobbies[moderatorLobby.Id].Status);
        Assert.Equal(LobbyStatus.Cancelled, lobbies[terminalLobby.Id].Status);
    }

    [Fact]
    public async Task ConcurrentCancellationsReloadAndReevaluateTheLobbyTransition()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var interceptor = new SynchronizeFirstTwoCancellationSavesInterceptor();
        await using var services = CreateServices(interceptor);
        var ownerId = Guid.NewGuid();
        Guid lobbyId;
        await using (var setupScope = services.CreateAsyncScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<LobbyDbContext>();
            await context.Database.MigrateAsync(timeout.Token);
            var createdLobby = new Lobby(Guid.NewGuid(), ownerId, 2, new RankRequirement("dota2", 1));
            context.Lobbies.Add(createdLobby);
            await context.SaveChangesAsync(timeout.Token);
            lobbyId = createdLobby.Id;
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = CancelInSeparateScopeAsync(services, lobbyId, ownerId, start.Task, timeout.Token);
        var second = CancelInSeparateScopeAsync(services, lobbyId, ownerId, start.Task, timeout.Token);
        start.SetResult();

        var results = await Task.WhenAll(first, second);

        Assert.Contains(results, result => result.Outcome == LobbyCancellationOutcome.Success);
        Assert.Contains(results, result => result.Outcome == LobbyCancellationOutcome.Rejected);
        Assert.Equal(2, interceptor.CancellationSaveCount);
        await using var verifyScope = services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<LobbyDbContext>();
        var lobby = await verifyContext.Lobbies.SingleAsync(candidate => candidate.Id == lobbyId, timeout.Token);
        Assert.Equal(LobbyStatus.Cancelled, lobby.Status);
    }

    [Fact]
    public async Task ConcurrentJoinsReloadAndReevaluateTheLobbyWithoutOverbooking()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var interceptor = new SynchronizeFirstTwoMembershipSavesInterceptor();
        await using var services = CreateServices(interceptor);
        Guid lobbyId;
        await using (var setupScope = services.CreateAsyncScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<LobbyDbContext>();
            var commands = setupScope.ServiceProvider.GetRequiredService<ILobbyCommandService>();
            await context.Database.MigrateAsync(timeout.Token);
            var created = await commands.CreateAsync(
                Guid.NewGuid(),
                new CreateLobbyRequest(2, "dota2", 1, null),
                timeout.Token);
            lobbyId = Assert.IsType<Guid>(created.LobbyId);
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = JoinInSeparateScopeAsync(services, lobbyId, start.Task, timeout.Token);
        var second = JoinInSeparateScopeAsync(services, lobbyId, start.Task, timeout.Token);
        start.SetResult();

        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.Equal(LobbyMembershipOutcome.Success, result.Outcome));
        await using var verifyScope = services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<LobbyDbContext>();
        var lobby = await verifyContext.Lobbies
            .Include("members")
            .SingleAsync(candidate => candidate.Id == lobbyId, timeout.Token);
        Assert.Equal(LobbyStatus.Full, lobby.Status);
        Assert.Equal(2, lobby.MembersCount);
        Assert.Equal(2, lobby.Members.Count);
        Assert.Equal(3, interceptor.MembershipSaveCount);
    }

    [Fact]
    public async Task FiftyConcurrentJoinsFillOnlyFiveSeatsAndPersistOneCompletionFact()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var interceptor = new RecordPersistedCompletionFactsInterceptor();
        await using var services = CreateServices(interceptor);
        Guid lobbyId;
        await using (var setupScope = services.CreateAsyncScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<LobbyDbContext>();
            var commands = setupScope.ServiceProvider.GetRequiredService<ILobbyCommandService>();
            await context.Database.MigrateAsync(timeout.Token);
            var created = await commands.CreateAsync(
                Guid.NewGuid(),
                new CreateLobbyRequest(5, "dota2", 1, null),
                timeout.Token);
            lobbyId = Assert.IsType<Guid>(created.LobbyId);
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var joins = Enumerable.Range(0, 50)
            .Select(_ => JoinInSeparateScopeAsync(services, lobbyId, start.Task, timeout.Token))
            .ToArray();
        start.SetResult();

        var results = await Task.WhenAll(joins);

        Assert.Equal(5, results.Count(result => result.Outcome == LobbyMembershipOutcome.Success));
        Assert.Equal(45, results.Count(result => result.Outcome == LobbyMembershipOutcome.Rejected));
        await using var verifyScope = services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<LobbyDbContext>();
        var lobby = await verifyContext.Lobbies
            .Include("members")
            .SingleAsync(candidate => candidate.Id == lobbyId, timeout.Token);
        Assert.Equal(LobbyStatus.Full, lobby.Status);
        Assert.Equal(5, lobby.MembersCount);
        Assert.Equal(5, lobby.Members.Count);
        var completion = Assert.Single(interceptor.PersistedCompletionFacts);
        Assert.Equal(lobbyId, completion.LobbyId);
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

    private ServiceProvider CreateServices(IInterceptor? interceptor = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LobbyDatabase"] = fixture.PostgreSql.GetConnectionString()
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        if (interceptor is not null)
        {
            services.AddSingleton<IInterceptor>(interceptor);
        }

        services.AddLobbyPersistence(configuration);
        services.AddLobbyInternalAuthentication(configuration);

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task<LobbyMembershipResult> JoinInSeparateScopeAsync(
        ServiceProvider services,
        Guid lobbyId,
        Task start,
        CancellationToken cancellationToken)
    {
        await start;
        await using var scope = services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<ILobbyCommandService>();
        return await commands.JoinAsync(
            lobbyId,
            Guid.NewGuid(),
            new JoinLobbyRequest(Guid.NewGuid().ToString("N"), "Concurrent Player", "dota2", 1),
            cancellationToken);
    }

    private static async Task<LobbyCancellationResult> CancelInSeparateScopeAsync(
        ServiceProvider services,
        Guid lobbyId,
        Guid ownerId,
        Task start,
        CancellationToken cancellationToken)
    {
        await start;
        await using var scope = services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<ILobbyCommandService>();
        return await commands.CancelAsync(lobbyId, CreateActor(ownerId, "Player"), cancellationToken);
    }

    private static ClaimsPrincipal CreateActor(Guid actorId, string role) => new(new ClaimsIdentity(
        [
            new Claim("sub", actorId.ToString("D")),
            new Claim("role", role),
            new Claim("scope", LobbyInternalAuthenticationExtensions.WritePolicy)
        ],
        "Test",
        "sub",
        "role"));

    private sealed class SynchronizeFirstTwoMembershipSavesInterceptor : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int membershipSaveCount;

        public int MembershipSaveCount => Volatile.Read(ref membershipSaveCount);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var isMembershipSave = eventData.Context?.ChangeTracker.Entries<LobbyMember>()
                .Any(entry => entry.State == EntityState.Added) == true;
            if (!isMembershipSave)
            {
                return result;
            }

            var membershipSaveNumber = Interlocked.Increment(ref membershipSaveCount);
            if (membershipSaveNumber <= 2)
            {
                if (membershipSaveNumber == 2)
                {
                    release.TrySetResult();
                }

                await release.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }

    private sealed class RecordPersistedCompletionFactsInterceptor : SaveChangesInterceptor
    {
        private readonly ConcurrentQueue<LobbyCompleted> persistedCompletionFacts = [];

        public IReadOnlyCollection<LobbyCompleted> PersistedCompletionFacts => persistedCompletionFacts;

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            RecordPersistedCompletionFacts(eventData.Context);
            return result;
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            RecordPersistedCompletionFacts(eventData.Context);
            return ValueTask.FromResult(result);
        }

        private void RecordPersistedCompletionFacts(DbContext? context)
        {
            if (context is null)
            {
                return;
            }

            foreach (var lobby in context.ChangeTracker.Entries<Lobby>().Select(entry => entry.Entity))
            {
                foreach (var completion in lobby.CompletedEvents)
                {
                    persistedCompletionFacts.Enqueue(completion);
                }
            }
        }
    }

    private sealed class SynchronizeFirstTwoCancellationSavesInterceptor : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int cancellationSaveCount;

        public int CancellationSaveCount => Volatile.Read(ref cancellationSaveCount);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var isCancellationSave = eventData.Context?.ChangeTracker.Entries<Lobby>()
                .Any(entry => entry.State == EntityState.Modified && entry.Entity.Status == LobbyStatus.Cancelled) == true;
            if (!isCancellationSave)
            {
                return result;
            }

            var cancellationSaveNumber = Interlocked.Increment(ref cancellationSaveCount);
            if (cancellationSaveNumber <= 2)
            {
                if (cancellationSaveNumber == 2)
                {
                    release.TrySetResult();
                }

                await release.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
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
