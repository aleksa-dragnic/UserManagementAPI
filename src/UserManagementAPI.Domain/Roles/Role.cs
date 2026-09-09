using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Domain.Roles;

/// <summary>
/// A container of permissions. The role itself carries no authority — it is the
/// permission codes it holds that policies check.
/// </summary>
public sealed class Role : AggregateRoot
{
    public const int MaxNameLength = 100;

    public static readonly Error NameEmpty = new("Role.NameEmpty", "Role name must be provided.");

    public static readonly Error NameTooLong = new(
        "Role.NameTooLong",
        $"Role name must not exceed {MaxNameLength} characters.");

    public static readonly Error PermissionAlreadyAdded = new(
        "Role.PermissionAlreadyAdded",
        "The role already holds this permission.");

    public static readonly Error PermissionNotHeld = new(
        "Role.PermissionNotHeld",
        "The role does not hold this permission.");

    public static readonly Error NotFound = new("Role.NotFound", "The role was not found.");

    private readonly List<RolePermission> _permissions = [];

    private Role(Guid id, string name)
        : base(id) => Name = name;

    /// <summary>Required by EF Core.</summary>
    private Role()
    {
    }

    public string Name { get; private set; } = null!;

    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    public static Result<Role> Create(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Role>(NameEmpty);
        }

        var trimmed = name.Trim();

        if (trimmed.Length > MaxNameLength)
        {
            return Result.Failure<Role>(NameTooLong);
        }

        return Result.Success(new Role(Guid.NewGuid(), trimmed));
    }

    public Result AddPermission(Guid permissionId)
    {
        if (_permissions.Any(permission => permission.PermissionId == permissionId))
        {
            return Result.Failure(PermissionAlreadyAdded);
        }

        _permissions.Add(new RolePermission(Id, permissionId));

        return Result.Success();
    }

    public Result RemovePermission(Guid permissionId)
    {
        var held = _permissions.FirstOrDefault(permission => permission.PermissionId == permissionId);

        if (held is null)
        {
            return Result.Failure(PermissionNotHeld);
        }

        _permissions.Remove(held);

        return Result.Success();
    }
}