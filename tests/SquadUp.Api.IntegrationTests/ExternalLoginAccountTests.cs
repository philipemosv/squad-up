using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadUp.Identity.Application;
using SquadUp.Identity.Infrastructure;

namespace SquadUp.Api.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class ExternalLoginAccountTests : IClassFixture<IdentityDatabaseFixture>
{
    private const string Discord = "Discord";
    private readonly IdentityDatabaseFixture fixture;

    public ExternalLoginAccountTests(IdentityDatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task UpsertCreatesOneAccountAndReusesIt()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var providerKey = SyntheticProviderKey();

        var first = await UpsertAsync(services, Discord, providerKey, timeout.Token);
        var second = await UpsertAsync(services, Discord, providerKey, timeout.Token);

        Assert.True(first.WasCreated);
        Assert.False(second.WasCreated);
        Assert.Equal(first.UserId, second.UserId);
        await AssertLoginStateAsync(services, providerKey, first.UserId, timeout.Token);
    }

    [Fact]
    public async Task ConcurrentUpsertConvergesWithoutOrphanUsers()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var providerKey = SyntheticProviderKey();
        var usersBefore = await CountUsersAsync(services, timeout.Token);

        var attempts = Enumerable.Range(0, 12)
            .Select(_ => UpsertAsync(services, Discord, providerKey, timeout.Token));
        var results = await Task.WhenAll(attempts);

        var userId = Assert.Single(results.Select(result => result.UserId).Distinct());
        Assert.Single(results, result => result.WasCreated);
        Assert.Equal(usersBefore + 1, await CountUsersAsync(services, timeout.Token));
        await AssertLoginStateAsync(services, providerKey, userId, timeout.Token);
    }

    [Fact]
    public async Task LinkRejectsExternalAndLocalAccountCollisions()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var firstKey = SyntheticProviderKey();
        var secondKey = SyntheticProviderKey();
        var unusedKey = SyntheticProviderKey();
        var first = await UpsertAsync(services, Discord, firstKey, timeout.Token);
        var second = await UpsertAsync(services, Discord, secondKey, timeout.Token);

        var externalCollision = await LinkAsync(
            services,
            second.UserId,
            Discord,
            firstKey,
            timeout.Token);
        var localCollision = await LinkAsync(
            services,
            first.UserId,
            Discord,
            unusedKey,
            timeout.Token);

        Assert.Equal(ExternalLoginLinkResult.ExternalLoginCollision, externalCollision);
        Assert.Equal(ExternalLoginLinkResult.ProviderAlreadyLinked, localCollision);
        await AssertLoginStateAsync(services, firstKey, first.UserId, timeout.Token);
        await AssertLoginStateAsync(services, secondKey, second.UserId, timeout.Token);
    }

    [Fact]
    public async Task ConcurrentLinksAllowOnlyOneLoginForTheSameProvider()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var account = await UpsertAsync(
            services,
            "SyntheticBootstrap",
            SyntheticProviderKey(),
            timeout.Token);

        var results = await Task.WhenAll(
            LinkAsync(
                services,
                account.UserId,
                Discord,
                SyntheticProviderKey(),
                timeout.Token),
            LinkAsync(
                services,
                account.UserId,
                Discord,
                SyntheticProviderKey(),
                timeout.Token));

        Assert.Contains(ExternalLoginLinkResult.Linked, results);
        Assert.Contains(ExternalLoginLinkResult.ProviderAlreadyLinked, results);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(1, await context.UserLogins.CountAsync(
            login => login.UserId == account.UserId && login.LoginProvider == Discord,
            timeout.Token));
    }

    [Fact]
    public async Task LinkAndUnlinkReportIdempotentAndMissingAccountOutcomes()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var providerKey = SyntheticProviderKey();
        var account = await UpsertAsync(services, Discord, providerKey, timeout.Token);

        var alreadyLinked = await LinkAsync(
            services,
            account.UserId,
            Discord,
            providerKey,
            timeout.Token);
        var notLinked = await UnlinkAsync(
            services,
            account.UserId,
            Discord,
            SyntheticProviderKey(),
            timeout.Token);
        var missingLinkAccount = await LinkAsync(
            services,
            Guid.CreateVersion7(),
            Discord,
            SyntheticProviderKey(),
            timeout.Token);
        var missingUnlinkAccount = await UnlinkAsync(
            services,
            Guid.CreateVersion7(),
            Discord,
            SyntheticProviderKey(),
            timeout.Token);

        Assert.Equal(ExternalLoginLinkResult.AlreadyLinked, alreadyLinked);
        Assert.Equal(ExternalLoginUnlinkResult.NotLinked, notLinked);
        Assert.Equal(ExternalLoginLinkResult.AccountNotFound, missingLinkAccount);
        Assert.Equal(ExternalLoginUnlinkResult.AccountNotFound, missingUnlinkAccount);
    }

    [Fact]
    public async Task UnlinkRefusesToOrphanAccountAndAllowsAnAlternativeLogin()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var discordKey = SyntheticProviderKey();
        var account = await UpsertAsync(services, Discord, discordKey, timeout.Token);

        var refused = await UnlinkAsync(
            services,
            account.UserId,
            Discord,
            discordKey,
            timeout.Token);
        var alternative = await LinkAsync(
            services,
            account.UserId,
            "SyntheticAlternative",
            SyntheticProviderKey(),
            timeout.Token);
        var removed = await UnlinkAsync(
            services,
            account.UserId,
            Discord,
            discordKey,
            timeout.Token);

        Assert.Equal(ExternalLoginUnlinkResult.WouldOrphanAccount, refused);
        Assert.Equal(ExternalLoginLinkResult.Linked, alternative);
        Assert.Equal(ExternalLoginUnlinkResult.Unlinked, removed);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.False(await context.UserLogins.AnyAsync(
            login => login.LoginProvider == Discord && login.ProviderKey == discordKey,
            timeout.Token));
        Assert.True(await context.Users.AnyAsync(user => user.Id == account.UserId, timeout.Token));
    }

    [Fact]
    public async Task ConcurrentUnlinksCannotRemoveEveryLoginMethod()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var services = CreateServices();
        await MigrateAsync(services, timeout.Token);
        var discordKey = SyntheticProviderKey();
        var alternativeKey = SyntheticProviderKey();
        var account = await UpsertAsync(services, Discord, discordKey, timeout.Token);
        Assert.Equal(
            ExternalLoginLinkResult.Linked,
            await LinkAsync(
                services,
                account.UserId,
                "SyntheticAlternative",
                alternativeKey,
                timeout.Token));

        var results = await Task.WhenAll(
            UnlinkAsync(services, account.UserId, Discord, discordKey, timeout.Token),
            UnlinkAsync(
                services,
                account.UserId,
                "SyntheticAlternative",
                alternativeKey,
                timeout.Token));

        Assert.Contains(ExternalLoginUnlinkResult.Unlinked, results);
        Assert.Contains(ExternalLoginUnlinkResult.WouldOrphanAccount, results);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(1, await context.UserLogins.CountAsync(
            login => login.UserId == account.UserId,
            timeout.Token));
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

    private static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<IdentityDbContext>()
            .Database
            .MigrateAsync(cancellationToken);
    }

    private static async Task<ExternalLoginUpsertResult> UpsertAsync(
        IServiceProvider services,
        string provider,
        string providerKey,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IExternalLoginAccountService>()
            .UpsertAsync(provider, providerKey, cancellationToken);
    }

    private static async Task<ExternalLoginLinkResult> LinkAsync(
        IServiceProvider services,
        Guid userId,
        string provider,
        string providerKey,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IExternalLoginAccountService>()
            .LinkAsync(userId, provider, providerKey, cancellationToken);
    }

    private static async Task<ExternalLoginUnlinkResult> UnlinkAsync(
        IServiceProvider services,
        Guid userId,
        string provider,
        string providerKey,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IExternalLoginAccountService>()
            .UnlinkAsync(userId, provider, providerKey, cancellationToken);
    }

    private static async Task AssertLoginStateAsync(
        IServiceProvider services,
        string providerKey,
        Guid expectedUserId,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var login = await context.UserLogins.SingleAsync(
            candidate => candidate.LoginProvider == Discord && candidate.ProviderKey == providerKey,
            cancellationToken);
        Assert.Equal(expectedUserId, login.UserId);
        var sessionClaims = await scope.ServiceProvider
            .GetRequiredService<IUserSessionClaimsProvider>()
            .FindAsync(expectedUserId, cancellationToken);
        Assert.NotNull(sessionClaims);
        Assert.Contains(SquadUpRoles.Player, sessionClaims.Roles);
    }

    private static async Task<int> CountUsersAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IdentityDbContext>()
            .Users
            .CountAsync(cancellationToken);
    }

    private static string SyntheticProviderKey() =>
        Random.Shared.NextInt64(10000000000000000, 99999999999999999)
            .ToString(CultureInfo.InvariantCulture);
}
