namespace SquadUp.LobbyService.Domain;

/// <summary>
/// Defines the catalog game and rank-ordinal range accepted by a lobby.
/// </summary>
public sealed record RankRequirement
{
    public const int MaxGameIdLength = 32;

    private RankRequirement()
    {
        GameId = string.Empty;
    }

    public RankRequirement(string gameId, int minimumOrdinal, int? maximumOrdinal = null)
    {
        GameId = NormalizeGameId(gameId);
        MinimumOrdinal = ValidatePositiveOrdinal(minimumOrdinal, nameof(minimumOrdinal));
        MaximumOrdinal = maximumOrdinal is null
            ? null
            : ValidateMaximumOrdinal(maximumOrdinal.Value, MinimumOrdinal);
    }

    public string GameId { get; private set; }

    public int MinimumOrdinal { get; private set; }

    public int? MaximumOrdinal { get; private set; }

    public bool IsSatisfiedBy(PlayerRank rank)
    {
        ArgumentNullException.ThrowIfNull(rank);

        return string.Equals(GameId, rank.GameId, StringComparison.Ordinal) &&
            rank.Ordinal >= MinimumOrdinal &&
            (MaximumOrdinal is null || rank.Ordinal <= MaximumOrdinal);
    }

    private static string NormalizeGameId(string gameId)
    {
        var normalized = gameId?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized.Length is 0 or > MaxGameIdLength
            ? throw new ArgumentException(
                $"Game id must be between 1 and {MaxGameIdLength} characters.",
                nameof(gameId))
            : normalized;
    }

    private static int ValidatePositiveOrdinal(int ordinal, string parameterName) => ordinal > 0
        ? ordinal
        : throw new ArgumentOutOfRangeException(parameterName, ordinal, "Rank ordinal must be positive.");

    private static int ValidateMaximumOrdinal(int maximumOrdinal, int minimumOrdinal)
    {
        ValidatePositiveOrdinal(maximumOrdinal, nameof(maximumOrdinal));

        return maximumOrdinal >= minimumOrdinal
            ? maximumOrdinal
            : throw new ArgumentOutOfRangeException(
                nameof(maximumOrdinal),
                maximumOrdinal,
                "Maximum rank ordinal must not be lower than the minimum rank ordinal.");
    }
}
