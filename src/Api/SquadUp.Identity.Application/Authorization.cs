namespace SquadUp.Identity.Application;

public static class SquadUpClaimTypes
{
    public const string Subject = "sub";
    public const string DiscordUserId = "discord_user_id";
    public const string Role = "role";
    public const string Scope = "scope";
}

public static class SquadUpRoles
{
    public const string Player = "Player";
    public const string Moderator = "Moderator";
    public const string Admin = "Admin";

    public static bool IsDefined(string role) => role is Player or Moderator or Admin;
}

public interface IUserSessionClaimsProvider
{
    public Task<UserSessionClaims?> FindAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record UserSessionClaims(Guid UserId, IReadOnlyCollection<string> Roles);
