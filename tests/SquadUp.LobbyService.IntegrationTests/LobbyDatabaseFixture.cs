using System.Security.Cryptography;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace SquadUp.LobbyService.IntegrationTests;

public sealed class LobbyDatabaseFixture : IAsyncLifetime
{
    private const string PostgreSqlImage =
        "postgres:17.10-bookworm@sha256:9b18b78397054fce88a9552e9d5a3ad5bb7fd258c5b3cc1c5028e46373d6ea8f";
    private const string RedisImage =
        "redis:8.4-bookworm@sha256:0a0f28c99ae50da4e0504499d2cd5b41746135c64f28ec42c88dafad93f60d41";

    public LobbyDatabaseFixture()
    {
        PostgreSql = new PostgreSqlBuilder(PostgreSqlImage)
            .WithDatabase("squadup_lobby_integration")
            .WithUsername("squadup_lobby")
            .WithPassword(RandomNumberGenerator.GetHexString(32))
            .Build();
        Redis = new RedisBuilder(RedisImage).Build();
    }

    public PostgreSqlContainer PostgreSql { get; }

    public RedisContainer Redis { get; }

    public Task InitializeAsync() => Task.WhenAll(PostgreSql.StartAsync(), Redis.StartAsync());

    public async Task DisposeAsync()
    {
        await Redis.DisposeAsync();
        await PostgreSql.DisposeAsync();
    }
}
