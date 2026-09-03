using System.Security.Cryptography;
using Testcontainers.PostgreSql;

namespace SquadUp.Api.IntegrationTests;

public sealed class ProfileDatabaseFixture : IAsyncLifetime
{
    private const string PostgreSqlImage =
        "postgres:17.10-bookworm@sha256:9b18b78397054fce88a9552e9d5a3ad5bb7fd258c5b3cc1c5028e46373d6ea8f";

    public ProfileDatabaseFixture()
    {
        PostgreSql = new PostgreSqlBuilder(PostgreSqlImage)
            .WithDatabase("squadup_profile_integration")
            .WithUsername("squadup_profile")
            .WithPassword(RandomNumberGenerator.GetHexString(32))
            .Build();
    }

    public PostgreSqlContainer PostgreSql { get; }

    public Task InitializeAsync() => PostgreSql.StartAsync();

    public Task DisposeAsync() => PostgreSql.DisposeAsync().AsTask();
}
