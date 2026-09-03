namespace SquadUp.Identity.Infrastructure;

internal sealed class IdentityPersistenceOptions
{
    internal const string ConnectionStringName = "IdentityDatabase";
    internal const string ValidationError =
        "ConnectionStrings:IdentityDatabase is required and must be a valid PostgreSQL connection string.";

    public string ConnectionString { get; set; } = string.Empty;
}
