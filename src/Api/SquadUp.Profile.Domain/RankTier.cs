namespace SquadUp.Profile.Domain;

public sealed class RankTier
{
    public const int MaxTierIdLength = 32;
    public const int MaxNameLength = 64;

    private RankTier()
    {
    }

    public RankTier(string gameId, string tierId, string name, int ordinal)
    {
        GameId = ValidateGameId(gameId);
        TierId = ValidateTierId(tierId);
        Name = ValidateName(name);
        Ordinal = ordinal > 0
            ? ordinal
            : throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, "Ordinal must be a positive number.");
        IsActive = true;
    }

    public string GameId { get; private set; } = string.Empty;

    public string TierId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int Ordinal { get; private set; }

    public bool IsActive { get; private set; }

    private static string ValidateGameId(string gameId)
    {
        var normalized = gameId?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized.Length is 0 or > Game.MaxIdLength
            ? throw new ArgumentException("Rank tier requires a valid game id.", nameof(gameId))
            : normalized;
    }

    private static string ValidateTierId(string tierId)
    {
        var normalized = tierId?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is 0 or > MaxTierIdLength)
        {
            throw new ArgumentException(
                $"Rank tier id must be between 1 and {MaxTierIdLength} characters.",
                nameof(tierId));
        }

        return normalized;
    }

    private static string ValidateName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaxNameLength)
        {
            throw new ArgumentException(
                $"Rank tier name must be between 1 and {MaxNameLength} characters.",
                nameof(name));
        }

        return trimmed;
    }
}
