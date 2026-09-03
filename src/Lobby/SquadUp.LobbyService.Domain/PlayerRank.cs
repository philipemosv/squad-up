namespace SquadUp.LobbyService.Domain;

/// <summary>
/// A player rank expressed with the ordinal owned by the game catalog.
/// </summary>
public sealed record PlayerRank
{
    public PlayerRank(string gameId, int ordinal)
    {
        GameId = NormalizeGameId(gameId);
        Ordinal = ordinal > 0
            ? ordinal
            : throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, "Rank ordinal must be positive.");
    }

    public string GameId { get; }

    public int Ordinal { get; }

    private static string NormalizeGameId(string gameId)
    {
        var normalized = gameId?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized.Length is 0 or > RankRequirement.MaxGameIdLength
            ? throw new ArgumentException(
                $"Game id must be between 1 and {RankRequirement.MaxGameIdLength} characters.",
                nameof(gameId))
            : normalized;
    }
}
