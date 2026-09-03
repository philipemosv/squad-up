using System.Security.Cryptography;

namespace SquadUp.LobbyService.Infrastructure;

internal sealed class LobbyInternalAuthenticationOptions
{
    public const string SectionName = "InternalAuthentication";
    public const string ValidationError =
        "InternalAuthentication must define a valid issuer, Lobby audience, API client, two-to-five-minute maximum token lifetime, allowlisted scopes, and one or more named RSA public keys of at least 2048 bits.";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string ApiClientId { get; set; } = string.Empty;

    public int MaximumTokenLifetimeSeconds { get; set; }

    public string[] AllowedScopes { get; set; } = [];

    public Dictionary<string, string> PublicKeys { get; set; } = [];

    public static bool IsValid(LobbyInternalAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!IsBoundedTokenValue(options.Issuer) ||
            !IsBoundedTokenValue(options.Audience) ||
            !IsBoundedTokenValue(options.ApiClientId) ||
            options.MaximumTokenLifetimeSeconds is < 120 or > 300 ||
            options.AllowedScopes.Length is 0 or > 16 ||
            options.AllowedScopes.Distinct(StringComparer.Ordinal).Count() != options.AllowedScopes.Length ||
            options.AllowedScopes.Any(scope => !IsBoundedTokenValue(scope)) ||
            options.PublicKeys.Count is 0 or > 5 ||
            options.PublicKeys.Keys.Any(keyId => !IsBoundedTokenValue(keyId)))
        {
            return false;
        }

        return options.PublicKeys.Values.All(IsValidPublicKey);
    }

    private static bool IsValidPublicKey(string publicKeyPem)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            if (rsa.KeySize < 2048)
            {
                return false;
            }

            try
            {
                _ = rsa.ExportParameters(includePrivateParameters: true);
                return false;
            }
            catch (CryptographicException)
            {
                return true;
            }
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
