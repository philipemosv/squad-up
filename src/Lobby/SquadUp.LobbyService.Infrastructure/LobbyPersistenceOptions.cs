namespace SquadUp.LobbyService.Infrastructure;

internal sealed class LobbyPersistenceOptions
{
    internal const string ConnectionStringName = "LobbyDatabase";
    internal const string ValidationError =
        "ConnectionStrings:LobbyDatabase is required and must be a valid PostgreSQL connection string.";

    public string ConnectionString { get; set; } = string.Empty;
}
