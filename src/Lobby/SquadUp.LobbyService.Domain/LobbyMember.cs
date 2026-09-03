namespace SquadUp.LobbyService.Domain;

/// <summary>
/// Stores the minimum participant snapshot needed to provision a match later.
/// </summary>
public sealed class LobbyMember
{
    public const int MaxDiscordUserIdLength = 32;
    public const int MaxDisplayNameLength = 32;

    public LobbyMember(Guid playerId, string discordUserId, string displayName, PlayerRank rank)
    {
        PlayerId = playerId != Guid.Empty
            ? playerId
            : throw new ArgumentException("Player id must not be empty.", nameof(playerId));
        DiscordUserId = ValidateRequired(discordUserId, MaxDiscordUserIdLength, nameof(discordUserId));
        DisplayName = ValidateRequired(displayName, MaxDisplayNameLength, nameof(displayName));
        Rank = rank ?? throw new ArgumentNullException(nameof(rank));
    }

    public Guid PlayerId { get; }

    public string DiscordUserId { get; }

    public string DisplayName { get; }

    public PlayerRank Rank { get; }

    private static string ValidateRequired(string value, int maximumLength, string parameterName)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 || trimmed.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Value must be between 1 and {maximumLength} characters.",
                parameterName);
        }

        return trimmed;
    }
}
