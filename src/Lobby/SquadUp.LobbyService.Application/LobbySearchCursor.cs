namespace SquadUp.LobbyService.Application;

public static class LobbySearchCursor
{
    public static string Encode(Guid lobbyId) => Convert.ToBase64String(lobbyId.ToByteArray())
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    public static bool TryDecode(string? cursor, out Guid lobbyId)
    {
        lobbyId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(cursor) || cursor.Length != 22 ||
            cursor.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(cursor.Replace('-', '+').Replace('_', '/') + "==");
            lobbyId = new Guid(bytes);
            return lobbyId != Guid.Empty;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
