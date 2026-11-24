using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.Infrastructure.Identity;

/// <summary>
/// Wrong passwords in a row, per email, in PostgreSQL (ADR 0024). After the
/// threshold, each further failure refuses sign-in for the email for a delay
/// that doubles - one second, two, four - up to the maximum. A success forgets
/// the run; so does a quiet day.
///
/// Every statement runs on a context of its own, committed at once: a refused
/// login answers a failed Result, and the transaction behavior rolls the
/// request's own work back, which must not take the count with it. The counter
/// is one INSERT … ON CONFLICT DO UPDATE, so two failures at once count twice
/// on any number of replicas.
/// </summary>
public sealed class LoginThrottle(
    DbContextOptions<AppDbContext> contextOptions,
    TimeProvider timeProvider,
    IOptions<LoginThrottleOptions> options) : ILoginThrottle
{
    private readonly LoginThrottleOptions _options = options.Value;

    public async Task<TimeSpan?> BlockedForAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var context = new AppDbContext(contextOptions);
        var hash = Hash(email);
        var now = Now();

        var blockedUntil = await context.Set<LoginFailure>()
            .AsNoTracking()
            .Where(failure => failure.EmailHash == hash)
            .Select(failure => failure.BlockedUntilUtc)
            .SingleOrDefaultAsync(cancellationToken);

        return blockedUntil is { } until && until > now ? until - now : null;
    }

    public async Task RecordFailureAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var context = new AppDbContext(contextOptions);
        var hash = Hash(email);
        var now = Now();
        var forgetBefore = now - _options.ForgetAfter;

        var failures = (await context.Database.SqlQuery<int>(
            $"""
            INSERT INTO login_failures (email_hash, consecutive_failures, last_failure_at_utc, blocked_until_utc)
            VALUES ({hash}, 1, {now}, NULL)
            ON CONFLICT (email_hash) DO UPDATE SET
                consecutive_failures = CASE
                    WHEN login_failures.last_failure_at_utc <= {forgetBefore} THEN 1
                    ELSE login_failures.consecutive_failures + 1
                END,
                last_failure_at_utc = {now}
            RETURNING consecutive_failures AS "Value"
            """).ToListAsync(cancellationToken)).Single();

        if (failures < _options.Threshold)
        {
            return;
        }

        // Never shortens a refusal already in place: two failures at once each
        // set one, and the longer wins.
        var until = now + DelayAfter(failures);

        await context.Database.ExecuteSqlAsync(
            $"""
            UPDATE login_failures
            SET blocked_until_utc = GREATEST(COALESCE(blocked_until_utc, {until}), {until})
            WHERE email_hash = {hash}
            """,
            cancellationToken);
    }

    public async Task ResetAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var context = new AppDbContext(contextOptions);
        var hash = Hash(email);

        await context.Database.ExecuteSqlAsync(
            $"DELETE FROM login_failures WHERE email_hash = {hash}",
            cancellationToken);
    }

    /// <summary>Deletes runs that are forgotten and no longer refuse anything; the cleanup service calls it.</summary>
    public async Task<int> DeleteForgottenAsync(CancellationToken cancellationToken = default)
    {
        await using var context = new AppDbContext(contextOptions);
        var now = Now();
        var forgetBefore = now - _options.ForgetAfter;

        return await context.Database.ExecuteSqlAsync(
            $"""
            DELETE FROM login_failures
            WHERE last_failure_at_utc <= {forgetBefore}
              AND (blocked_until_utc IS NULL OR blocked_until_utc <= {now})
            """,
            cancellationToken);
    }

    /// <summary>One second at the threshold, doubling with each failure after it, up to the maximum.</summary>
    public TimeSpan DelayAfter(int failures)
    {
        var doublings = Math.Min(failures - _options.Threshold, 30);
        var delay = TimeSpan.FromSeconds(Math.Pow(2, doublings));

        return delay < _options.MaxDelay ? delay : _options.MaxDelay;
    }

    /// <summary>The same email typed differently is the same email; the hash keeps no address.</summary>
    private static string Hash(string email) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant())));

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
