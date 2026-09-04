using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace SquadUp.LobbyService.Infrastructure;

public sealed record HttpIdempotencyResponse(
    int StatusCode,
    string? Code = null,
    string? Detail = null,
    string? Location = null,
    string? Body = null);

public interface IHttpIdempotencyLedger
{
    public const int MaximumKeyLength = 128;

    public Task<HttpIdempotencyResponse> ExecuteAsync(
        Guid ownerPlayerId,
        string key,
        byte[] requestHash,
        Func<CancellationToken, Task<HttpIdempotencyResponse>> operation,
        CancellationToken cancellationToken);
}

internal sealed class HttpIdempotencyLedger(LobbyDbContext context) : IHttpIdempotencyLedger
{
    internal static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public async Task<HttpIdempotencyResponse> ExecuteAsync(
        Guid ownerPlayerId,
        string key,
        byte[] requestHash,
        Func<CancellationToken, Task<HttpIdempotencyResponse>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(requestHash);
        ArgumentNullException.ThrowIfNull(operation);

        var now = DateTimeOffset.UtcNow;
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await context.IdempotencyKeys
            .Where(record => record.ExpiresAtUtc <= now)
            .ExecuteDeleteAsync(cancellationToken);

        // The advisory lock turns a duplicate request into a replay instead of a unique-key race.
        await context.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(hashtext({0}), hashtext({1}))",
            [key, ownerPlayerId.ToString("D")],
            cancellationToken);

        var existing = await context.IdempotencyKeys.SingleOrDefaultAsync(
            record => record.OwnerPlayerId == ownerPlayerId && record.Key == key,
            cancellationToken);
        if (existing is not null)
        {
            if (!CryptographicOperations.FixedTimeEquals(existing.RequestHash, requestHash))
            {
                throw new HttpIdempotencyConflictException();
            }

            await transaction.CommitAsync(cancellationToken);
            return existing.ToResponse();
        }

        var record = new HttpIdempotencyKey(ownerPlayerId, key, requestHash, now.Add(Retention));
        context.IdempotencyKeys.Add(record);

        var response = await operation(cancellationToken);
        record.Complete(response);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }
}

public sealed class HttpIdempotencyConflictException : Exception
{
    public HttpIdempotencyConflictException()
        : base("The Idempotency-Key was already used for a different request.")
    {
    }
}
