using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SquadUp.LobbyService.Infrastructure;

public sealed class LobbyDbContextFactory : IDesignTimeDbContextFactory<LobbyDbContext>
{
    public LobbyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LobbyDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Port=5432;Database=squadup_lobby_design",
                npgsql => npgsql.MigrationsHistoryTable(
                    LobbyDbContext.MigrationsHistoryTable,
                    LobbyDbContext.SchemaName))
            .Options;

        return new LobbyDbContext(options);
    }
}
