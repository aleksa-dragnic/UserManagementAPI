using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Infrastructure.Persistence;

/// <summary>
/// The single unit of work over PostgreSQL. Mapping lives in
/// IEntityTypeConfiguration classes rather than attributes, so no persistence
/// concern reaches back into the domain (ADR 0004).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // citext gives case-insensitive uniqueness on email without a functional
        // index and without LOWER() on every lookup.
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}