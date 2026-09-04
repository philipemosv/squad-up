namespace SquadUp.LobbyService.Application;

public sealed record CreateLobbyRequest(
    int Capacity,
    string GameId,
    int MinimumRankOrdinal,
    int? MaximumRankOrdinal);

public enum CreateLobbyOutcome
{
    Success,
    ValidationFailed,
    GameNotFound,
    RankTierNotFound
}

public sealed record CreateLobbyResult(
    CreateLobbyOutcome Outcome,
    Guid? LobbyId = null,
    string? Error = null)
{
    public static CreateLobbyResult Success(Guid lobbyId) => new(CreateLobbyOutcome.Success, lobbyId);

    public static CreateLobbyResult Failed(CreateLobbyOutcome outcome, string error) =>
        new(outcome, Error: error);
}

/// <summary>
/// Contains only the deliberately publishable fields needed to discover a recruiting lobby.
/// </summary>
public sealed record LobbySummaryDto(
    Guid LobbyId,
    int Capacity,
    int MembersCount,
    string GameId,
    int MinimumRankOrdinal,
    int? MaximumRankOrdinal);
