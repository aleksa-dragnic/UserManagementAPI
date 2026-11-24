using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.Infrastructure.Idempotency;

/// <summary>
/// The idempotency keys, in PostgreSQL (ADR 0023). Every write is its own
/// statement, committed on its own, outside any request transaction: a
/// concurrent request with the same key has to see the claim at once, and a
/// request whose own transaction rolls back must still release its key.
///
/// Raw SQL rather than the change tracker, because a claim is a conditional
/// insert and the database has to decide it: INSERT … ON CONFLICT DO NOTHING
/// lets exactly one of two concurrent requests in, on any number of replicas.
/// </summary>
public sealed class IdempotencyStore(
    AppDbContext context,
    TimeProvider timeProvider,
    IOptions<IdempotencyOptions> options) : IIdempotencyStore
{
    private readonly IdempotencyOptions _options = options.Value;

    public async Task<IdempotencyClaim> ClaimAsync(
        IdempotencyScope scope,
        string requestHash,
        CancellationToken cancellationToken = default)
    {
        // A row released between the two statements below leaves nothing to
        // read; the next round claims it. Three rounds is two more than that
        // race can need.
        for (var round = 0; round < 3; round++)
        {
            var now = Now();
            var expires = now + _options.Retention;
            var abandonedBefore = now - _options.Lease;

            var inserted = await context.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO idempotency_keys (caller_id, operation, key, request_hash, status, created_at_utc, expires_at_utc)
                VALUES ({scope.CallerId}, {scope.Operation}, {scope.Key}, {requestHash}, {IdempotencyRecord.InProgress}, {now}, {expires})
                ON CONFLICT (caller_id, operation, key) DO NOTHING
                """,
                cancellationToken);

            if (inserted == 1)
            {
                return new IdempotencyClaim.Claimed();
            }

            // The key is taken. It is this request's anyway if it has expired, or
            // if its claim is older than the lease and never completed: the
            // request that made it is gone, and the key must not stay locked
            // until it expires.
            var takenOver = await context.Database.ExecuteSqlAsync(
                $"""
                UPDATE idempotency_keys
                SET request_hash = {requestHash}, status = {IdempotencyRecord.InProgress},
                    created_at_utc = {now}, expires_at_utc = {expires}, completed_at_utc = NULL,
                    response_status = NULL, response_location = NULL, response_content_type = NULL, response_body = NULL
                WHERE caller_id = {scope.CallerId} AND operation = {scope.Operation} AND key = {scope.Key}
                  AND (expires_at_utc <= {now}
                       OR (status = {IdempotencyRecord.InProgress} AND created_at_utc <= {abandonedBefore}))
                """,
                cancellationToken);

            if (takenOver == 1)
            {
                return new IdempotencyClaim.Claimed();
            }

            var existing = await context.Set<IdempotencyRecord>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    record => record.CallerId == scope.CallerId &&
                              record.Operation == scope.Operation &&
                              record.Key == scope.Key,
                    cancellationToken);

            if (existing is null)
            {
                continue;
            }

            if (existing.Status == IdempotencyRecord.InProgress)
            {
                return new IdempotencyClaim.InProgress();
            }

            if (existing.RequestHash != requestHash)
            {
                return new IdempotencyClaim.Mismatch();
            }

            return new IdempotencyClaim.Replay(new StoredResponse(
                existing.ResponseStatus ?? 200,
                existing.ResponseLocation,
                existing.ResponseContentType,
                existing.ResponseBody ?? []));
        }

        return new IdempotencyClaim.InProgress();
    }

    public async Task CompleteAsync(
        IdempotencyScope scope,
        StoredResponse response,
        CancellationToken cancellationToken = default)
    {
        var now = Now();

        await context.Database.ExecuteSqlAsync(
            $"""
            UPDATE idempotency_keys
            SET status = {IdempotencyRecord.Completed}, completed_at_utc = {now},
                response_status = {response.StatusCode}, response_location = {response.Location},
                response_content_type = {response.ContentType}, response_body = {response.Body}
            WHERE caller_id = {scope.CallerId} AND operation = {scope.Operation} AND key = {scope.Key}
              AND status = {IdempotencyRecord.InProgress}
            """,
            cancellationToken);
    }

    public async Task ReleaseAsync(IdempotencyScope scope, CancellationToken cancellationToken = default) =>
        await context.Database.ExecuteSqlAsync(
            $"""
            DELETE FROM idempotency_keys
            WHERE caller_id = {scope.CallerId} AND operation = {scope.Operation} AND key = {scope.Key}
              AND status = {IdempotencyRecord.InProgress}
            """,
            cancellationToken);

    /// <summary>Deletes every expired key; the cleanup service calls it.</summary>
    public async Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = Now();

        return await context.Database.ExecuteSqlAsync(
            $"DELETE FROM idempotency_keys WHERE expires_at_utc <= {now}",
            cancellationToken);
    }

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
