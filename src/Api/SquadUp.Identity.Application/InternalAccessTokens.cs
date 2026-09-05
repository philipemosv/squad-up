namespace SquadUp.Identity.Application;

public interface IInternalAccessTokenIssuer
{
    public string Issue(InternalAccessTokenRequest request);

    public string IssueLobbyDelegatedToken(
        Guid delegatedUserId,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> scopes);
}

public sealed record InternalAccessTokenRequest(
    string Audience,
    string ClientId,
    IReadOnlyCollection<string> Scopes,
    Guid? DelegatedUserId = null,
    IReadOnlyCollection<string>? Roles = null);
