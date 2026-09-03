namespace SquadUp.Profile.Domain;

public static class GameRegion
{
    public static readonly IReadOnlyCollection<string> Allowed =
    [
        "NA", "SA", "WEU", "EEU", "SEA", "CHINA", "OCE"
    ];

    public static string Validate(string? region)
    {
        var normalized = region?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!Allowed.Contains(normalized))
        {
            throw new ArgumentException(
                $"Region must be one of: {string.Join(", ", Allowed)}.",
                nameof(region));
        }

        return normalized;
    }
}
