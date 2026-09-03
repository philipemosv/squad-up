using System.Text;
using Npgsql;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace SquadUp.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class LocalPlatformSmokeTests : IClassFixture<LocalPlatformFixture>
{
    private readonly LocalPlatformFixture fixture;

    public LocalPlatformSmokeTests(LocalPlatformFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task PostgreSqlAcceptsAuthenticatedReadWrite()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var connection = new NpgsqlConnection(fixture.PostgreSql.GetConnectionString());
        await connection.OpenAsync(timeout.Token);

        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TEMP TABLE smoke (value integer); " +
            "INSERT INTO smoke VALUES (1); SELECT value FROM smoke;";

        var result = await command.ExecuteScalarAsync(timeout.Token);

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task RabbitMqAcceptsAuthenticatedPublishAndConsume()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(fixture.RabbitMq.GetConnectionString())
        };

        await using var connection = await connectionFactory.CreateConnectionAsync(
            timeout.Token);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: timeout.Token);
        var queue = await channel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: null,
            cancellationToken: timeout.Token);
        var body = Encoding.UTF8.GetBytes("squad-up-smoke");

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queue.QueueName,
            mandatory: true,
            body: body,
            cancellationToken: timeout.Token);
        var delivery = await channel.BasicGetAsync(
            queue.QueueName,
            autoAck: true,
            timeout.Token);

        Assert.NotNull(delivery);
        Assert.Equal(body, delivery.Body.ToArray());
    }

    [Fact]
    public async Task RedisAcceptsAuthenticatedReadWrite()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var connection = await ConnectionMultiplexer
            .ConnectAsync(fixture.RedisConnectionString)
            .WaitAsync(timeout.Token);
        var database = connection.GetDatabase();
        var key = $"squad-up:integration:smoke:{Guid.NewGuid():N}";

        try
        {
            Assert.True(await database
                .StringSetAsync(key, "ok", TimeSpan.FromMinutes(1))
                .WaitAsync(timeout.Token));
            Assert.Equal("ok", await database.StringGetAsync(key).WaitAsync(timeout.Token));
        }
        finally
        {
            await database.KeyDeleteAsync(key).WaitAsync(timeout.Token);
        }
    }
}
