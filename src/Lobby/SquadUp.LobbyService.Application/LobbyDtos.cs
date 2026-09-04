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
/// Contains a participant snapshot supplied by the authenticated internal boundary.
/// The caller identity is deliberately a separate command parameter.
/// </summary>
public sealed record JoinLobbyRequest(
    string DiscordUserId,
    string DisplayName,
    string GameId,
    int RankOrdinal);

public enum LobbyMembershipOutcome
{
    Success,
    LobbyNotFound,
    ValidationFailed,
    Rejected,
    ConcurrencyConflict
}

public sealed record LobbyMembershipResult(
    LobbyMembershipOutcome Outcome,
    string? Error = null)
{
    public static LobbyMembershipResult Success() => new(LobbyMembershipOutcome.Success);

    public static LobbyMembershipResult Failed(LobbyMembershipOutcome outcome, string error) =>
        new(outcome, error);
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
