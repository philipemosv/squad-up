namespace SquadUp.Profile.Domain;

public sealed class PlayerGame
{
    private PlayerGame()
    {
    }

    public PlayerGame(Guid playerId, RankTier rankTier, string region, DateTimeOffset? verifiedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(rankTier);

        PlayerId = playerId != Guid.Empty
            ? playerId
            : throw new ArgumentException("Player id must not be empty.", nameof(playerId));
        GameId = rankTier.GameId;
        RankTierId = rankTier.TierId;
        Region = GameRegion.Validate(region);
        VerifiedAtUtc = verifiedAtUtc;
    }

    public Guid PlayerId { get; private set; }

    public string GameId { get; private set; } = string.Empty;

    public string RankTierId { get; private set; } = string.Empty;

    public string Region { get; private set; } = string.Empty;

    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    public void UpdateRank(RankTier rankTier, string region)
    {
        ArgumentNullException.ThrowIfNull(rankTier);
        if (!string.Equals(rankTier.GameId, GameId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Rank tier '{rankTier.TierId}' belongs to game '{rankTier.GameId}', not '{GameId}'.");
        }

        RankTierId = rankTier.TierId;
        Region = GameRegion.Validate(region);
    }
}
