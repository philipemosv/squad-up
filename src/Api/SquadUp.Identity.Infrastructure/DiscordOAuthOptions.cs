namespace SquadUp.Identity.Infrastructure;

internal sealed class DiscordOAuthOptions
{
    public const string ClientIdValidationError =
        "Discord:ClientId must contain only the numeric Discord application identifier.";
    public const string ClientSecretValidationError =
        "Discord:ClientSecret must be between 32 and 256 characters.";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}
