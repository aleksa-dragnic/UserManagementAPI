using System.Linq.Expressions;

using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Users.Queries.GetUsers;

/// <summary>
/// Filter, search and sort for the user collection, as plain LINQ over
/// IQueryable&lt;User&gt; — no EF Core here, so the rules are unit-testable in
/// memory and the handler in Infrastructure composes them into one SQL query.
///
/// Nothing user-supplied is ever turned into an expression: the status is
/// matched against the enumeration, the search term becomes a parameter, and the
/// sort clause picks from a fixed dictionary of fields.
/// </summary>
public static class UserQueryExtensions
{
    private delegate IOrderedQueryable<User> SortStep(IQueryable<User> source, IOrderedQueryable<User>? ordered, bool descending);

    private static readonly Dictionary<string, SortStep> SortableFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["email"] = (source, ordered, descending) => Order(source, ordered, user => user.Email.Value, descending),
        ["firstName"] = (source, ordered, descending) => Order(source, ordered, user => user.Name.First, descending),
        ["lastName"] = (source, ordered, descending) => Order(source, ordered, user => user.Name.Last, descending),
        ["status"] = (source, ordered, descending) => Order(source, ordered, user => user.Status, descending),
        ["createdAt"] = (source, ordered, descending) => Order(source, ordered, user => user.CreatedAtUtc, descending)
    };

    /// <summary>The fields a client may sort by.</summary>
    public static IReadOnlyCollection<string> SortableFieldNames => SortableFields.Keys;

    /// <summary>
    /// Keeps the users in the given state. A blank status keeps everyone; an
    /// unknown one never gets this far — the validator refuses it.
    /// </summary>
    public static IQueryable<User> Filter(this IQueryable<User> users, string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return users;
        }

        var match = Enumeration.GetAll<UserStatus>()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, status.Trim(), StringComparison.OrdinalIgnoreCase));

        return match is null ? users : users.Where(user => user.Status == match);
    }

    /// <summary>
    /// Case-insensitive substring match on email, first name and last name.
    /// Emails are stored lowercase already (Email normalises on creation), so
    /// only the names are lowered; in SQL that is lower(name_first), with the
    /// term as a parameter.
    /// </summary>
    public static IQueryable<User> Search(this IQueryable<User> users, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return users;
        }

        var term = searchTerm.Trim().ToLowerInvariant();

        return users.Where(user =>
            user.Email.Value.Contains(term) ||
            user.Name.First.ToLower().Contains(term) ||
            user.Name.Last.ToLower().Contains(term));
    }

    /// <summary>
    /// Applies "field [asc|desc]" clauses in order, taking only fields from the
    /// whitelist. With no usable clause the order is email ascending. The id is
    /// always the last key, so two users with the same sort value keep a stable
    /// position and a page never repeats or skips a row.
    /// </summary>
    public static IQueryable<User> Sort(this IQueryable<User> users, string? orderBy)
    {
        IOrderedQueryable<User>? ordered = null;

        foreach (var (field, descending) in ParseOrderBy(orderBy))
        {
            ordered = SortableFields[field](users, ordered, descending);
        }

        ordered ??= users.OrderBy(user => user.Email.Value);

        return ordered.ThenBy(user => user.Id);
    }

    private static List<(string Field, bool Descending)> ParseOrderBy(string? orderBy)
    {
        var clauses = new List<(string Field, bool Descending)>();

        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return clauses;
        }

        foreach (var clause in orderBy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = clause.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length is 0 or > 2 || !SortableFields.ContainsKey(parts[0]))
            {
                continue;
            }

            if (clauses.Exists(existing => string.Equals(existing.Field, parts[0], StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var direction = parts.Length == 2 ? parts[1] : "asc";

            if (!direction.Equals("asc", StringComparison.OrdinalIgnoreCase) &&
                !direction.Equals("desc", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            clauses.Add((parts[0], direction.Equals("desc", StringComparison.OrdinalIgnoreCase)));
        }

        return clauses;
    }

    private static IOrderedQueryable<User> Order<TKey>(
        IQueryable<User> source,
        IOrderedQueryable<User>? ordered,
        Expression<Func<User, TKey>> key,
        bool descending) =>
        ordered is null
            ? (descending ? source.OrderByDescending(key) : source.OrderBy(key))
            : (descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key));
}