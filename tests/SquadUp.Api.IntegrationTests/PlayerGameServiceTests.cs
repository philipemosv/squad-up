using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadUp.Profile.Application;
using SquadUp.Profile.Infrastructure;

namespace SquadUp.Api.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class PlayerGameServiceTests : IClassFixture<ProfileDatabaseFixture>
{
    private readonly ProfileDatabaseFixture fixture;

    public PlayerGameServiceTests(ProfileDatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task UpsertRejectsAGameThatIsNotInTheCatalog()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var playerId = await CreateProfileAsync(services, timeout.Token);

        var result = await UpsertGameAsync(
            services,
            playerId,
            "not-a-real-game",
            new PutPlayerGameRequest("immortal", "SA"),
            timeout.Token);

        Assert.Equal(PlayerGameMutationOutcome.GameNotFound, result.Outcome);
    }

    [Fact]
    public async Task UpsertRejectsARankTierThatIsNotInTheCatalogForThatGame()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var playerId = await CreateProfileAsync(services, timeout.Token);

        var result = await UpsertGameAsync(
            services,
            playerId,
            "dota2",
            new PutPlayerGameRequest("not-a-real-tier", "SA"),
            timeout.Token);

        Assert.Equal(PlayerGameMutationOutcome.RankTierNotFound, result.Outcome);
    }

    [Fact]
    public async Task UpsertRequiresAnExistingProfile()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);

        var result = await UpsertGameAsync(
            services,
            Guid.CreateVersion7(),
            "dota2",
            new PutPlayerGameRequest("immortal", "SA"),
            timeout.Token);

        Assert.Equal(PlayerGameMutationOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task UpsertCreatesAGameThatIsReturnedByList()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var playerId = await CreateProfileAsync(services, timeout.Token);

        var result = await UpsertGameAsync(
            services,
            playerId,
            "dota2",
            new PutPlayerGameRequest("immortal", "sa"),
            timeout.Token);
        var games = await ListAsync(services, playerId, timeout.Token);

        Assert.Equal(PlayerGameMutationOutcome.Success, result.Outcome);
        Assert.Equal("Dota 2", result.Game!.GameName);
        Assert.Equal("Immortal", result.Game.RankTierName);
        Assert.Equal(8, result.Game.RankOrdinal);
        Assert.Equal("SA", result.Game.Region);
        var game = Assert.Single(games);
        Assert.Equal("dota2", game.GameId);
        Assert.Equal("immortal", game.RankTierId);
    }

    [Fact]
    public async Task UpsertOnAnExistingGameUpdatesTheRankInPlace()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var playerId = await CreateProfileAsync(services, timeout.Token);
        await UpsertGameAsync(
            services,
            playerId,
            "dota2",
            new PutPlayerGameRequest("herald", "NA"),
            timeout.Token);

        await UpsertGameAsync(
            services,
            playerId,
            "dota2",
            new PutPlayerGameRequest("immortal", "SA"),
            timeout.Token);
        var games = await ListAsync(services, playerId, timeout.Token);

        var game = Assert.Single(games);
        Assert.Equal("immortal", game.RankTierId);
        Assert.Equal("SA", game.Region);
    }

    [Fact]
    public async Task RemoveReportsNotFoundForAGameThePlayerNeverAdded()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var playerId = await CreateProfileAsync(services, timeout.Token);

        var outcome = await RemoveAsync(services, playerId, "dota2", timeout.Token);

        Assert.Equal(PlayerGameRemovalOutcome.NotFound, outcome);
    }

    [Fact]
    public async Task RemoveDeletesTheGameSoListNoLongerReturnsIt()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var playerId = await CreateProfileAsync(services, timeout.Token);
        await UpsertGameAsync(
            services,
            playerId,
            "dota2",
            new PutPlayerGameRequest("immortal", "SA"),
            timeout.Token);

        var outcome = await RemoveAsync(services, playerId, "dota2", timeout.Token);
        var games = await ListAsync(services, playerId, timeout.Token);

        Assert.Equal(PlayerGameRemovalOutcome.Removed, outcome);
        Assert.Empty(games);
    }

    [Fact]
    public async Task TwoPlayersGamesAreFullyIsolated()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var playerA = await CreateProfileAsync(services, timeout.Token);
        var playerB = await CreateProfileAsync(services, timeout.Token);
        await UpsertGameAsync(
            services,
            playerA,
            "dota2",
            new PutPlayerGameRequest("immortal", "SA"),
            timeout.Token);

        var gamesForB = await ListAsync(services, playerB, timeout.Token);
        var removalForB = await RemoveAsync(services, playerB, "dota2", timeout.Token);
        var gamesForA = await ListAsync(services, playerA, timeout.Token);

        Assert.Empty(gamesForB);
        Assert.Equal(PlayerGameRemovalOutcome.NotFound, removalForB);
        Assert.Single(gamesForA);
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

    private static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<ProfileDbContext>()
            .Database
            .MigrateAsync(cancellationToken);
    }

    private static async Task<Guid> CreateProfileAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var playerId = Guid.CreateVersion7();
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<IPlayerProfileService>()
            .UpsertAsync(playerId, new UpdateProfileRequest("Nickname", null, null, null), cancellationToken);
        Assert.Equal(ProfileMutationOutcome.Success, result.Outcome);
        return playerId;
    }

    private static async Task<IReadOnlyList<PlayerGameDto>> ListAsync(
        IServiceProvider services,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IPlayerGameService>()
            .ListAsync(playerId, cancellationToken);
    }

    private static async Task<PlayerGameMutationResult> UpsertGameAsync(
        IServiceProvider services,
        Guid playerId,
        string gameId,
        PutPlayerGameRequest request,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IPlayerGameService>()
            .UpsertAsync(playerId, gameId, request, cancellationToken);
    }

    private static async Task<PlayerGameRemovalOutcome> RemoveAsync(
        IServiceProvider services,
        Guid playerId,
        string gameId,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IPlayerGameService>()
            .RemoveAsync(playerId, gameId, cancellationToken);
    }
}
