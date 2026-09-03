namespace SquadUp.Profile.Infrastructure;

internal sealed class ProfilePersistenceOptions
{
    internal const string ConnectionStringName = "ProfileDatabase";
    internal const string ValidationError =
        "ConnectionStrings:ProfileDatabase is required and must be a valid PostgreSQL connection string.";

    public string ConnectionString { get; set; } = string.Empty;
}
