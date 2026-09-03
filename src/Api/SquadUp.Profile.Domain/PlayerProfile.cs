namespace SquadUp.Profile.Domain;

public enum ProfileStatus
{
    Active,
    Hidden
}

public sealed class PlayerProfile
{
    public const int MinNicknameLength = 3;
    public const int MaxNicknameLength = 32;
    public const int MaxTimeZoneIdLength = 64;

    private PlayerProfile()
    {
    }

    public PlayerProfile(Guid playerId, string nickname, string? timeZoneId)
    {
        PlayerId = playerId != Guid.Empty
            ? playerId
            : throw new ArgumentException("Player id must not be empty.", nameof(playerId));
        Nickname = ValidateNickname(nickname);
        TimeZoneId = ValidateTimeZoneId(timeZoneId);
        Status = ProfileStatus.Active;
    }

    public Guid PlayerId { get; private set; }

    public string Nickname { get; private set; } = string.Empty;

    public string? TimeZoneId { get; private set; }

    public ProfileStatus Status { get; private set; }

    public void Update(string nickname, string? timeZoneId, ProfileStatus status)
    {
        Nickname = ValidateNickname(nickname);
        TimeZoneId = ValidateTimeZoneId(timeZoneId);
        Status = status;
    }

    private static string ValidateNickname(string nickname)
    {
        var trimmed = nickname?.Trim() ?? string.Empty;
        if (trimmed.Length is < MinNicknameLength or > MaxNicknameLength)
        {
            throw new ArgumentException(
                $"Nickname must be between {MinNicknameLength} and {MaxNicknameLength} characters.",
                nameof(nickname));
        }

        return trimmed;
    }

    private static string? ValidateTimeZoneId(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        var trimmed = timeZoneId.Trim();
        if (trimmed.Length > MaxTimeZoneIdLength)
        {
            throw new ArgumentException(
                $"Time zone id must be at most {MaxTimeZoneIdLength} characters.",
                nameof(timeZoneId));
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(trimmed);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException($"'{trimmed}' is not a recognized IANA time zone id.", nameof(timeZoneId));
        }

        return trimmed;
    }
}
