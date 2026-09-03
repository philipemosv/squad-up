using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadUp.Profile.Application;
using SquadUp.Profile.Infrastructure;

namespace SquadUp.Api.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class PlayerProfileServiceTests : IClassFixture<ProfileDatabaseFixture>
{
    private readonly ProfileDatabaseFixture fixture;

    public PlayerProfileServiceTests(ProfileDatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task GetReturnsNullWhenNoProfileExists()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);

        var profile = await GetAsync(services, Guid.CreateVersion7(), timeout.Token);

        Assert.Null(profile);
    }

    [Fact]
    public async Task UpsertCreatesAProfileWithoutRequiringAVersion()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var playerId = Guid.CreateVersion7();

        var result = await UpsertAsync(
            services,
            playerId,
            new UpdateProfileRequest("Nickname", "America/Sao_Paulo", null, null),
            timeout.Token);

        Assert.Equal(ProfileMutationOutcome.Success, result.Outcome);
        Assert.Equal(playerId, result.Profile!.PlayerId);
        Assert.Equal("Nickname", result.Profile.Nickname);
        Assert.Equal("America/Sao_Paulo", result.Profile.TimeZoneId);
        Assert.Equal("Active", result.Profile.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Profile.Version));
    }

    [Fact]
    public async Task UpsertOfAnExistingProfileWithoutAnExpectedVersionIsRejected()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var playerId = Guid.CreateVersion7();
        await UpsertAsync(
            services,
            playerId,
            new UpdateProfileRequest("Nickname", null, null, null),
            timeout.Token);

        var result = await UpsertAsync(
            services,
            playerId,
            new UpdateProfileRequest("NewNickname", null, null, null),
            timeout.Token);

        Assert.Equal(ProfileMutationOutcome.VersionRequired, result.Outcome);
        var unchanged = await GetAsync(services, playerId, timeout.Token);
        Assert.Equal("Nickname", unchanged!.Nickname);
    }

    [Fact]
    public async Task UpsertWithAStaleVersionIsRejectedAndDoesNotOverwriteTheWinner()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var playerId = Guid.CreateVersion7();
        var created = await UpsertAsync(
            services,
            playerId,
            new UpdateProfileRequest("Nickname", null, null, null),
            timeout.Token);
        var staleVersion = created.Profile!.Version;
        var winner = await UpsertAsync(
            services,
            playerId,
            new UpdateProfileRequest("WinnerNickname", null, null, staleVersion),
            timeout.Token);

        var loser = await UpsertAsync(
            services,
            playerId,
            new UpdateProfileRequest("LoserNickname", null, null, staleVersion),
            timeout.Token);

        Assert.Equal(ProfileMutationOutcome.Success, winner.Outcome);
        Assert.Equal(ProfileMutationOutcome.VersionConflict, loser.Outcome);
        var current = await GetAsync(services, playerId, timeout.Token);
        Assert.Equal("WinnerNickname", current!.Nickname);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("this-nickname-is-far-too-long-for-the-limit")]
    [InlineData("   ")]
    public async Task UpsertRejectsNicknamesOutsideTheAllowedLength(string nickname)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);

        var result = await UpsertAsync(
            services,
            Guid.CreateVersion7(),
            new UpdateProfileRequest(nickname, null, null, null),
            timeout.Token);

        Assert.Equal(ProfileMutationOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task UpsertRejectsAnUnrecognizedTimeZoneId()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);

        var result = await UpsertAsync(
            services,
            Guid.CreateVersion7(),
            new UpdateProfileRequest("Nickname", "Not/A_Real_Zone", null, null),
            timeout.Token);

        Assert.Equal(ProfileMutationOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task TwoPlayersHaveFullyIsolatedProfiles()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var playerA = Guid.CreateVersion7();
        var playerB = Guid.CreateVersion7();

        await UpsertAsync(
            services,
            playerA,
            new UpdateProfileRequest("PlayerA", null, null, null),
            timeout.Token);
        await UpsertAsync(
            services,
            playerB,
            new UpdateProfileRequest("PlayerB", null, null, null),
            timeout.Token);

        var profileA = await GetAsync(services, playerA, timeout.Token);
        var profileB = await GetAsync(services, playerB, timeout.Token);
        Assert.Equal("PlayerA", profileA!.Nickname);
        Assert.Equal("PlayerB", profileB!.Nickname);
        Assert.NotEqual(profileA.PlayerId, profileB.PlayerId);
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

    private static async Task<ProfileDto?> GetAsync(
        IServiceProvider services,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IPlayerProfileService>()
            .GetAsync(playerId, cancellationToken);
    }

    private static async Task<ProfileMutationResult> UpsertAsync(
        IServiceProvider services,
        Guid playerId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IPlayerProfileService>()
            .UpsertAsync(playerId, request, cancellationToken);
    }
}
