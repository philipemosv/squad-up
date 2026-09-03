namespace SquadUp.Profile.Application;

public sealed record PlayerGameDto(
    string GameId,
    string GameName,
    string RankTierId,
    string RankTierName,
    int RankOrdinal,
    string Region,
    DateTimeOffset? VerifiedAtUtc);

public sealed record PutPlayerGameRequest(string RankTierId, string Region);

public enum PlayerGameMutationOutcome
{
    Success,
    ValidationFailed,
    GameNotFound,
    RankTierNotFound
}

public sealed record PlayerGameMutationResult(
    PlayerGameMutationOutcome Outcome,
    PlayerGameDto? Game = null,
    string? Error = null)
{
    public static PlayerGameMutationResult Success(PlayerGameDto game) =>
        new(PlayerGameMutationOutcome.Success, game);

    public static PlayerGameMutationResult Failed(PlayerGameMutationOutcome outcome, string error) =>
        new(outcome, Error: error);
}

public enum PlayerGameRemovalOutcome
{
    Removed,
    NotFound
}
