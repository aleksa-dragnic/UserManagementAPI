using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Auth;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.Infrastructure.Outbox;

namespace UserManagementAPI.Infrastructure.Persistence;

/// <summary>
/// The single unit of work over PostgreSQL. Mapping lives in
/// IEntityTypeConfiguration classes rather than attributes, so no persistence
/// concern reaches back into the domain (ADR 0004).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Opens the transaction inside the configured execution strategy. With
    /// EnableRetryOnFailure on (ADR 0002), a bare BeginTransaction throws at
    /// runtime; the strategy re-runs the whole operation on a transient failure,
    /// which is what makes a Neon wake-up invisible to the caller.
    /// </summary>
    public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
        Func<Task<TResponse>> operation,
        CancellationToken cancellationToken = default)
        where TResponse : Result
    {
        var strategy = Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
            async token =>
            {
                await using var transaction = await Database.BeginTransactionAsync(token);

                var response = await operation();

                if (response.IsSuccess)
                {
                    await transaction.CommitAsync(token);
                }
                else
                {
                    await transaction.RollbackAsync(token);
                }

                return response;
            },
            cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // citext gives case-insensitive uniqueness on email without a functional
        // index and without LOWER() on every lookup.
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}