using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

using Npgsql;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Queries.GetUsers;
using UserManagementAPI.Domain.Auth;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.Infrastructure.Auditing;
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

    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

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

    /// <summary>
    /// The unique index on users.email (InitialSchema). Registration and an
    /// email change check for a taken address first; two requests that pass the
    /// check together are separated here, by the index.
    /// </summary>
    private const string UserEmailIndex = "ix_users_email";

    /// <summary>
    /// Translates the EF concurrency failure into an application exception, so
    /// the one place that knows about optimistic concurrency is the persistence
    /// layer and every caller sees a type it is allowed to name. A unique
    /// violation on the email index becomes the domain error the handlers'
    /// own check returns, so a lost race still answers 409 rather than 500.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(
                "The record was modified by another request. Read it again and retry.",
                exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UserEmailIndex
        })
        {
            throw new DomainException(User.EmailNotUnique, exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // citext gives case-insensitive uniqueness on email without a functional
        // index and without LOWER() on every lookup.
        modelBuilder.HasPostgresExtension("citext");

        // unaccent folds diacritics for the user search (ADR 0020). SearchText.Fold
        // is plain C# in the Application, so the rule stays testable in memory;
        // against the database it becomes lower(unaccent(...)) on the column.
        modelBuilder.HasPostgresExtension("unaccent");

        modelBuilder.HasDbFunction(typeof(SearchText).GetMethod(nameof(SearchText.Fold), [typeof(string)])!)
            .HasTranslation(arguments => new SqlFunctionExpression(
                "lower",
                [
                    new SqlFunctionExpression(
                        "unaccent",
                        arguments,
                        nullable: true,
                        argumentsPropagateNullability: [true],
                        typeof(string),
                        arguments[0].TypeMapping)
                ],
                nullable: true,
                argumentsPropagateNullability: [true],
                typeof(string),
                arguments[0].TypeMapping));

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}