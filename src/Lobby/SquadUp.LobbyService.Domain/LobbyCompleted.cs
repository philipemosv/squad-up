namespace SquadUp.LobbyService.Domain;

/// <summary>
/// A domain fact emitted once when the lobby first reaches its configured capacity.
/// </summary>
public sealed record LobbyCompleted(Guid LobbyId);
