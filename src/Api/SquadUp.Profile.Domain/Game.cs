namespace SquadUp.Profile.Domain;

public sealed class Game
{
    public const int MaxIdLength = 32;
    public const int MaxNameLength = 64;

    private Game()
    {
    }

    public Game(string id, string name)
    {
        Id = ValidateId(id);
        Name = ValidateName(name);
        IsActive = true;
    }

    public string Id { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    private static string ValidateId(string id)
    {
        var normalized = id?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is 0 or > MaxIdLength)
        {
            throw new ArgumentException(
                $"Game id must be between 1 and {MaxIdLength} characters.",
                nameof(id));
        }

        return normalized;
    }

    private static string ValidateName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaxNameLength)
        {
            throw new ArgumentException(
                $"Game name must be between 1 and {MaxNameLength} characters.",
                nameof(name));
        }

        return trimmed;
    }
}
