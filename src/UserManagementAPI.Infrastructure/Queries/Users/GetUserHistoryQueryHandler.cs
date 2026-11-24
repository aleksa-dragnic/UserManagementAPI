using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Dtos;
using UserManagementAPI.Application.Users.Queries.GetUserHistory;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.Infrastructure.Auditing;
using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.Infrastructure.Queries.Users;

/// <summary>
/// A user's history from the audit log (ADR 0025). Three sources: the rows for
/// the user itself; the role assignments that name the user in their payload;
/// and role assignments written before they did, tied to the user through
/// user_roles while the role is still held - a removal from before then cannot
/// be tied to anyone, and is not reported. Actor emails and role names are read
/// at the time of the request, so a role renamed since reads by its new name.
/// </summary>
internal sealed class GetUserHistoryQueryHandler(AppDbContext context)
    : IQueryHandler<GetUserHistoryQuery, IReadOnlyList<UserHistoryEntryDto>>
{
    private const string UserEntity = nameof(User);

    private const string UserRoleEntity = nameof(UserRole);

    /// <summary>The audit log's field names, as the API's contracts name them.</summary>
    private static readonly Dictionary<string, string> PublicFieldNames = new(StringComparer.Ordinal)
    {
        ["Email.Value"] = "email",
        ["Name.First"] = "firstName",
        ["Name.Last"] = "lastName",
        ["PasswordHash.Value"] = "password",
        ["Status"] = "status"
    };

    /// <summary>Fields every write moves; they say nothing about what changed.</summary>
    private static readonly HashSet<string> Bookkeeping = new(StringComparer.Ordinal)
    {
        "CreatedAtUtc",
        "UpdatedAtUtc"
    };

    public async Task<Result<IReadOnlyList<UserHistoryEntryDto>>> HandleAsync(
        GetUserHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!await context.Users.AnyAsync(user => user.Id == query.UserId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<UserHistoryEntryDto>>(User.NotFound);
        }

        var userId = query.UserId.ToString();
        var naming = JsonSerializer.Serialize(new { userId = query.UserId });

        var own = await context.AuditLog
            .AsNoTracking()
            .Where(entry => entry.Entity == UserEntity && entry.EntityId == userId)
            .OrderByDescending(entry => entry.OccurredAtUtc).ThenByDescending(entry => entry.Id)
            .Take(GetUserHistoryQuery.Limit)
            .Select(entry => new Row(entry.Id, entry.Action, entry.Entity, entry.Payload, entry.ActorId, entry.OccurredAtUtc, null))
            .ToListAsync(cancellationToken);

        var roles = await context.AuditLog
            .AsNoTracking()
            .Where(entry => entry.Entity == UserRoleEntity && EF.Functions.JsonContains(entry.Payload, naming))
            .OrderByDescending(entry => entry.OccurredAtUtc).ThenByDescending(entry => entry.Id)
            .Take(GetUserHistoryQuery.Limit)
            .Select(entry => new Row(entry.Id, entry.Action, entry.Entity, entry.Payload, entry.ActorId, entry.OccurredAtUtc, null))
            .ToListAsync(cancellationToken);

        var earlier = await (
                from entry in context.AuditLog.AsNoTracking()
                where entry.Entity == UserRoleEntity && !EF.Functions.JsonExists(entry.Payload, "userId")
                join assignment in context.Set<UserRole>().AsNoTracking()
                    on entry.EntityId equals assignment.Id.ToString()
                where assignment.UserId == query.UserId
                orderby entry.OccurredAtUtc descending, entry.Id descending
                select new Row(entry.Id, entry.Action, entry.Entity, entry.Payload, entry.ActorId, entry.OccurredAtUtc, assignment.RoleId))
            .Take(GetUserHistoryQuery.Limit)
            .ToListAsync(cancellationToken);

        var rows = own.Concat(roles).Concat(earlier)
            .OrderByDescending(row => row.OccurredAtUtc).ThenByDescending(row => row.Id)
            .Select(Interpret)
            .OfType<Interpreted>()
            .Take(GetUserHistoryQuery.Limit)
            .ToList();

        var actorIds = rows.Select(row => row.ActorId).OfType<Guid>().Distinct().ToList();
        var actors = await context.Users
            .AsNoTracking()
            .Where(user => actorIds.Contains(user.Id))
            .Select(user => new { user.Id, Email = user.Email.Value })
            .ToDictionaryAsync(user => user.Id, user => user.Email, cancellationToken);

        var roleIds = rows.Select(row => row.RoleId).OfType<Guid>().Distinct().ToList();
        var roleNames = await context.Roles
            .AsNoTracking()
            .Where(role => roleIds.Contains(role.Id))
            .Select(role => new { role.Id, role.Name })
            .ToDictionaryAsync(role => role.Id, role => role.Name, cancellationToken);

        IReadOnlyList<UserHistoryEntryDto> history = rows
            .Select(row => new UserHistoryEntryDto(
                row.OccurredAtUtc,
                row.Action,
                row.Fields,
                row.RoleId,
                row.RoleId is { } roleId && roleNames.TryGetValue(roleId, out var name) ? name : null,
                row.ActorId,
                row.ActorId is { } actorId && actors.TryGetValue(actorId, out var email) ? email : null))
            .ToList();

        return Result.Success(history);
    }

    /// <summary>
    /// What a row says about the user, or null when it says nothing: an update
    /// whose only fields are bookkeeping is the user's side of a role change,
    /// which its own row reports.
    /// </summary>
    private static Interpreted? Interpret(Row row)
    {
        using var payload = JsonDocument.Parse(row.Payload);
        var root = payload.RootElement;

        if (row.Entity == UserRoleEntity)
        {
            var action = row.Action switch
            {
                "Created" => UserHistoryActions.RoleAssigned,
                "Deleted" => UserHistoryActions.RoleRemoved,
                _ => null
            };
            var roleId = root.TryGetProperty("roleId", out var named) ? named.GetGuid() : row.RoleId;

            return action is null ? null : new Interpreted(row, action, [], roleId);
        }

        if (row.Action == "Created")
        {
            return new Interpreted(row, UserHistoryActions.Registered, [], null);
        }

        if (row.Action == "Deleted")
        {
            return new Interpreted(row, UserHistoryActions.Deleted, [], null);
        }

        var fields = root.TryGetProperty("changedFields", out var changed)
            ? changed.EnumerateArray()
                .Select(field => field.GetString()!)
                .Where(field => !Bookkeeping.Contains(field))
                .Select(field => PublicFieldNames.GetValueOrDefault(field, JsonNamingPolicy.CamelCase.ConvertName(field)))
                .Distinct()
                .ToList()
            : [];

        return fields.Count == 0 ? null : new Interpreted(row, UserHistoryActions.Updated, fields, null);
    }

    private sealed record Row(
        long Id,
        string Action,
        string Entity,
        string Payload,
        Guid? ActorId,
        DateTime OccurredAtUtc,
        Guid? RoleId);

    private sealed record Interpreted(
        DateTime OccurredAtUtc,
        string Action,
        IReadOnlyList<string> Fields,
        Guid? RoleId,
        Guid? ActorId)
    {
        public Interpreted(Row row, string action, IReadOnlyList<string> fields, Guid? roleId)
            : this(row.OccurredAtUtc, action, fields, roleId, row.ActorId)
        {
        }
    }
}
