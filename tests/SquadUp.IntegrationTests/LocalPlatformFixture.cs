using System.Security.Cryptography;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace SquadUp.IntegrationTests;

public sealed class LocalPlatformFixture : IAsyncLifetime
{
    private const string PostgreSqlImage =
        "postgres:17.10-bookworm@sha256:9b18b78397054fce88a9552e9d5a3ad5bb7fd258c5b3cc1c5028e46373d6ea8f";
    private const string RabbitMqImage =
        "rabbitmq:4.2.9-management@sha256:05a8dd87c954fffbdb34698f70a9de479239b38890694efa6a58dcfb792de1bf";
    private const string RedisImage =
        "redis:7.4.11-alpine@sha256:ff02b58f971e7d7d156a1267e283fcbbeee91773b6aa36c49dac28ecfe28eadf";

    private readonly string redisPassword = RandomNumberGenerator.GetHexString(32);

    public LocalPlatformFixture()
    {
        var postgreSqlPassword = RandomNumberGenerator.GetHexString(32);
        var rabbitMqPassword = RandomNumberGenerator.GetHexString(32);

        PostgreSql = new PostgreSqlBuilder(PostgreSqlImage)
            .WithDatabase("squadup_integration")
            .WithUsername("squadup")
            .WithPassword(postgreSqlPassword)
            .Build();

        RabbitMq = new RabbitMqBuilder(RabbitMqImage)
            .WithUsername("squadup")
            .WithPassword(rabbitMqPassword)
            .Build();

        Redis = new RedisBuilder(RedisImage)
            .WithCommand("redis-server", "--appendonly", "no", "--requirepass", redisPassword)
            .Build();
    }

    public PostgreSqlContainer PostgreSql { get; }

    public RabbitMqContainer RabbitMq { get; }

    public RedisContainer Redis { get; }

    public string RedisConnectionString
    {
        get
        {
            var configuration = StackExchange.Redis.ConfigurationOptions.Parse(Redis.GetConnectionString());
            configuration.Password = redisPassword;
            configuration.AbortOnConnectFail = true;
            configuration.ConnectTimeout = 5_000;
            return configuration.ToString(includePassword: true);
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            await Task.WhenAll(
                PostgreSql.StartAsync(),
                RabbitMq.StartAsync(),
                Redis.StartAsync());
        }
        catch
        {
            try
            {
                await DisposeContainersAsync();
            }
            catch
            {
                // Preserve the startup failure; Testcontainers reports cleanup separately.
            }

            throw;
        }
    }

    public Task DisposeAsync() => DisposeContainersAsync();

    private Task DisposeContainersAsync() => Task.WhenAll(
        Redis.DisposeAsync().AsTask(),
        RabbitMq.DisposeAsync().AsTask(),
        PostgreSql.DisposeAsync().AsTask());
}
