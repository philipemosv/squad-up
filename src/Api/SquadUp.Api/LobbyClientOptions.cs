namespace SquadUp.Api;

internal sealed class LobbyClientOptions
{
    internal const string SectionName = "LobbyClient";
    internal const string ValidationError =
        "LobbyClient:BaseAddress must be an absolute HTTPS address without user info, query, or fragment.";

    public string BaseAddress { get; set; } = string.Empty;

    internal static bool IsValid(LobbyClientOptions options) =>
        Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var baseAddress) &&
        baseAddress.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(baseAddress.UserInfo) &&
        string.IsNullOrEmpty(baseAddress.Query) &&
        string.IsNullOrEmpty(baseAddress.Fragment);
}
