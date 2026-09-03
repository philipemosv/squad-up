namespace SquadUp.LobbyService.Api;

internal sealed class LobbyHostOptions
{
    internal const string SectionName = "Lobby";
    internal const string ValidationError =
        "Lobby:ServiceName is required and must contain at most 64 ASCII letters, digits, dots, hyphens, or underscores.";

    public string ServiceName { get; set; } = string.Empty;

    internal static bool IsValid(LobbyHostOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ServiceName) || options.ServiceName.Length > 64)
        {
            return false;
        }

        return options.ServiceName.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');
    }
}
