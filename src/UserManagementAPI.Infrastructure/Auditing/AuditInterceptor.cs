using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Auth;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.Infrastructure.Outbox;
using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.Infrastructure.Auditing;

/// <summary>
/// Records who changed what, in the same SaveChanges as the change itself, so an
/// audit row and the write it describes commit or roll back together.
///
/// Runs after the domain event dispatch interceptor, so anything an event
/// handler changed is audited too. Owned value objects (Email, PersonName,
/// PasswordHash) are attributed to their owner as "Email.Value" and never get
/// an entry of their own. The payload lists the names of changed fields, never
/// their values.
///
/// A role assignment (UserRole) also names the user and the role it joins, as
/// "userId" and "roleId": references, not values, so a removal can still be
/// tied to its user and role once the user_roles row is gone (ADR 0025).
///
/// Not audited: the audit log itself, outbox messages (infrastructure plumbing)
/// and refresh tokens (rotated every fifteen minutes per session; the security
/// events that matter — login, reuse detection — are logged, not audited).
/// </summary>
internal sealed class AuditInterceptor(ICurrentUser currentUser, TimeProvider timeProvider) : SaveChangesInterceptor
{
    private static readonly HashSet<Type> Excluded =
    [
        typeof(AuditLogEntry),
        typeof(OutboxMessage),
        typeof(RefreshToken)
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Record(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Record(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Record(DbContext? context)
    {
        if (context is not AppDbContext appContext)
        {
            return;
        }

        // Interceptors run before EF's own change detection.
        appContext.ChangeTracker.DetectChanges();

        var pending = new Dictionary<(string Entity, string EntityId), (string Action, SortedSet<string> Fields)>();
        var assignments = new Dictionary<(string Entity, string EntityId), (Guid UserId, Guid RoleId)>();

        foreach (var entry in appContext.ChangeTracker.Entries().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var resolved = ResolveOwner(entry, appContext.ChangeTracker);

            if (resolved is null || Excluded.Contains(resolved.Value.Owner.Metadata.ClrType))
            {
                continue;
            }

            var (owner, prefix) = resolved.Value;
            var key = (owner.Metadata.ClrType.Name, KeyOf(owner));
            var action = ActionOf(owner.State);

            if (!pending.TryGetValue(key, out var accumulated))
            {
                accumulated = (action, new SortedSet<string>(StringComparer.Ordinal));
                pending[key] = accumulated;
            }

            if (owner.Entity is UserRole assignment)
            {
                assignments[key] = (assignment.UserId, assignment.RoleId);
            }

            if (Rank(action) > Rank(accumulated.Action))
            {
                pending[key] = (action, accumulated.Fields);
            }

            if (owner.State == EntityState.Added || owner.State == EntityState.Deleted)
            {
                continue;
            }

            foreach (var field in ChangedFields(entry, appContext.ChangeTracker))
            {
                accumulated.Fields.Add(prefix + field);
            }
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var ((entity, entityId), (action, fields)) in pending)
        {
            var payload = assignments.TryGetValue((entity, entityId), out var joins)
                ? JsonSerializer.Serialize(new { changedFields = fields, userId = joins.UserId, roleId = joins.RoleId }, SerializerOptions)
                : JsonSerializer.Serialize(new { changedFields = fields }, SerializerOptions);

            appContext.AuditLog.Add(new AuditLogEntry(currentUser.UserId, action, entity, entityId, payload, now));
        }
    }

    /// <summary>
    /// For an ordinary entity, itself. For an owned type, the tracked entry of
    /// its owner plus a prefix ("Email.") for the field names. Owned entries
    /// share their owner's key, which is how the owner is found.
    /// </summary>
    private static (EntityEntry Owner, string Prefix)? ResolveOwner(EntityEntry entry, ChangeTracker tracker)
    {
        if (!entry.Metadata.IsOwned())
        {
            return (entry, string.Empty);
        }

        var ownership = entry.Metadata.FindOwnership()!;
        var foreignKeyValues = ownership.Properties
            .Select(property => entry.Property(property.Name).CurrentValue)
            .ToArray();
        var principalKey = ownership.PrincipalKey.Properties;

        var owner = tracker.Entries().FirstOrDefault(candidate =>
            candidate.Metadata == ownership.PrincipalEntityType &&
            principalKey
                .Select((property, index) => Equals(candidate.Property(property.Name).CurrentValue, foreignKeyValues[index]))
                .All(matches => matches));

        if (owner is null)
        {
            return null;
        }

        var navigation = ownership.PrincipalToDependent?.Name ?? entry.Metadata.ShortName();

        return (owner, navigation + ".");
    }

    /// <summary>
    /// Modified: the properties EF marked modified whose value moved. Added or Deleted owned
    /// entries — EF replaces an owned instance rather than mutating it — count
    /// as every property of the owned type having changed.
    /// </summary>
    private static IEnumerable<string> ChangedFields(EntityEntry entry, ChangeTracker tracker)
    {
        var properties = entry.Properties.Where(property => !property.Metadata.IsShadowProperty());

        // A replaced owned instance is a Deleted entry and an Added one under the
        // same owner. The Added one reports the fields whose value differs from
        // its twin's; the Deleted one reports nothing, so a new last name is not
        // also a new first name.
        if (entry.Metadata.IsOwned() && entry.State is EntityState.Added or EntityState.Deleted)
        {
            var twin = TwinOf(entry, tracker);

            if (twin is not null)
            {
                return entry.State == EntityState.Deleted
                    ? []
                    : properties
                        .Where(property => !Equals(property.CurrentValue, twin.Property(property.Metadata.Name).OriginalValue))
                        .Select(property => property.Metadata.Name)
                        .ToList();
            }
        }

        // EF replaces an owned value object whole, so a new last name marks the
        // first name modified too; a field whose value did not move is not a change.
        return entry.State == EntityState.Modified
            ? properties
                .Where(property => property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
                .Select(property => property.Metadata.Name)
            : properties.Select(property => property.Metadata.Name);
    }

    /// <summary>The other half of a replaced owned instance: same type, same owner, opposite state.</summary>
    private static EntityEntry? TwinOf(EntityEntry entry, ChangeTracker tracker)
    {
        var opposite = entry.State == EntityState.Added ? EntityState.Deleted : EntityState.Added;
        var ownerKey = entry.Metadata.FindOwnership()!.Properties;

        return tracker.Entries().FirstOrDefault(candidate =>
            candidate.Metadata == entry.Metadata &&
            candidate.State == opposite &&
            ownerKey.All(property => Equals(
                candidate.Property(property.Name).CurrentValue,
                entry.Property(property.Name).CurrentValue)));
    }

    private static string KeyOf(EntityEntry entry) =>
        string.Join(":", entry.Metadata.FindPrimaryKey()!.Properties
            .Select(property => entry.Property(property.Name).CurrentValue?.ToString() ?? "null"));

    private static string ActionOf(EntityState state) => state switch
    {
        EntityState.Added => "Created",
        EntityState.Deleted => "Deleted",
        _ => "Updated"
    };

    private static int Rank(string action) => action switch
    {
        "Created" => 2,
        "Deleted" => 1,
        _ => 0
    };
}