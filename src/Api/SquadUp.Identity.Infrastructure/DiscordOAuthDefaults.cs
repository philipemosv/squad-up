namespace SquadUp.Identity.Infrastructure;

public static class DiscordOAuthDefaults
{
    public const string AuthenticationScheme = "Discord";
    public const string DisplayName = "Discord";
    public const string ConfigurationSection = "Discord";
    public const string AuthorizationEndpoint = "https://discord.com/oauth2/authorize";
    public const string TokenEndpoint = "https://discord.com/api/oauth2/token";
    public const string UserInformationEndpoint = "https://discord.com/api/users/@me";
    public const string CallbackPath = "/auth/discord/callback";
    public const string CompletionPath = "/auth/discord/complete";
    public const string Scope = "identify";

    public static bool IsValidUserId(string? userId) =>
        userId is not null &&
        userId.Length is >= 17 and <= 20 &&
        userId.All(char.IsAsciiDigit);
}
