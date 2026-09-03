using System.Security.Cryptography;

namespace SquadUp.Identity.Infrastructure;

internal sealed class InternalTokenOptions
{
    public const string SectionName = "InternalTokens";
    public const string ValidationError =
        "InternalTokens must define a valid issuer, Lobby audience, API client, two-to-five-minute lifetime, active RSA key ID, RSA private key of at least 2048 bits, and allowlisted scopes.";

    public string Issuer { get; set; } = string.Empty;

    public string LobbyAudience { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public int LifetimeSeconds { get; set; }

    public string ActiveKeyId { get; set; } = string.Empty;

    public string PrivateKeyPem { get; set; } = string.Empty;

    public string[] AllowedScopes { get; set; } = [];

    public static bool IsValid(InternalTokenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!IsBoundedTokenValue(options.Issuer) ||
            !IsBoundedTokenValue(options.LobbyAudience) ||
            !IsBoundedTokenValue(options.ClientId) ||
            !IsBoundedTokenValue(options.ActiveKeyId) ||
            options.LifetimeSeconds is < 120 or > 300 ||
            options.AllowedScopes.Length is 0 or > 16 ||
            options.AllowedScopes.Distinct(StringComparer.Ordinal).Count() != options.AllowedScopes.Length ||
            options.AllowedScopes.Any(scope => !IsBoundedTokenValue(scope)))
        {
            return false;
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(options.PrivateKeyPem);
            _ = rsa.ExportParameters(includePrivateParameters: true);
            return rsa.KeySize >= 2048;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static bool IsBoundedTokenValue(string? value) =>
        value is not null &&
        value.Length is >= 1 and <= 128 &&
        value.All(character => character is >= '!' and <= '~');
}
