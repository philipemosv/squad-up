namespace SquadUp.LobbyService.Infrastructure;

public sealed class HttpIdempotencyKey
{
    private HttpIdempotencyKey()
    {
    }

    public HttpIdempotencyKey(
        Guid ownerPlayerId,
        string key,
        byte[] requestHash,
        DateTimeOffset expiresAtUtc)
    {
        OwnerPlayerId = ownerPlayerId;
        Key = key;
        RequestHash = requestHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid OwnerPlayerId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public byte[] RequestHash { get; private set; } = [];

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public int? ResponseStatusCode { get; private set; }

    public string? ResponseCode { get; private set; }

    public string? ResponseDetail { get; private set; }

    public string? ResponseLocation { get; private set; }

    public string? ResponseBody { get; private set; }

    public void Complete(HttpIdempotencyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        ResponseStatusCode = response.StatusCode;
        ResponseCode = response.Code;
        ResponseDetail = response.Detail;
        ResponseLocation = response.Location;
        ResponseBody = response.Body;
    }

    public HttpIdempotencyResponse ToResponse() => new(
        ResponseStatusCode ?? throw new InvalidOperationException("The idempotency record has no completed response."),
        ResponseCode,
        ResponseDetail,
        ResponseLocation,
        ResponseBody);
}
