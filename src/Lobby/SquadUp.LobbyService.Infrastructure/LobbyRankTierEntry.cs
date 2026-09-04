namespace SquadUp.LobbyService.Infrastructure;

internal sealed class LobbyRankTierEntry
{
    public string GameId { get; set; } = string.Empty;

    public string TierId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Ordinal { get; set; }

    public bool IsActive { get; set; }
}
