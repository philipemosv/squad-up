using Microsoft.Extensions.Logging;

namespace SquadUp.Identity.Infrastructure;

internal static partial class DiscordOAuthLog
{
    [LoggerMessage(
        EventId = 2000,
        EventName = "DiscordOAuthCallbackFailed",
        Level = LogLevel.Warning,
        Message = "Discord OAuth callback failed.")]
    public static partial void CallbackFailed(ILogger logger);
}
