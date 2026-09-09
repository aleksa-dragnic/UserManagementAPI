using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users.Events;

namespace UserManagementAPI.Domain.Users;

/// <summary>
/// The user aggregate root. Every state transition goes through a method that
/// states which states it is legal from; there is not one public setter.
/// </summary>
public sealed class User : AggregateRoot
{
    public static readonly Error AlreadyVerified = new(
        "User.AlreadyVerified",
        "Only a pending user can have their email verified.");

    public static readonly Error Deactivated = new(
        "User.Deactivated",
        "A deactivated user cannot be modified.");

    public static readonly Error AlreadyLocked = new("User.AlreadyLocked", "The user is already locked.");

    public static readonly Error NotLocked = new("User.NotLocked", "The user is not locked.");

    public static readonly Error AlreadyDeactivated = new(
        "User.AlreadyDeactivated",
        "The user is already deactivated.");

    public static readonly Error NotFound = new("User.NotFound", "The user was not found.");

    public static readonly Error EmailNotUnique = new(
        "User.EmailNotUnique",
        "A user with this email already exists.");

    public static readonly Error RoleAlreadyAssigned = new(
        "User.RoleAlreadyAssigned",
        "The user already holds this role.");

    public static readonly Error LastRoleCannotBeRemoved = new(
        "User.LastRoleCannotBeRemoved",
        "A user must retain at least one role.");

    public static readonly Error RoleNotAssigned = new(
        "User.RoleNotAssigned",
        "The user does not hold this role.");

    private readonly List<UserRole> _roles = [];

    private User(Guid id, Email email, PersonName name, PasswordHash passwordHash)
        : base(id)
    {
        Email = email;
        Name = name;
        PasswordHash = passwordHash;
        Status = UserStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    /// <summary>Required by EF Core.</summary>
    private User()
    {
    }

    public Email Email { get; private set; } = null!;

    public PersonName Name { get; private set; } = null!;

    public PasswordHash PasswordHash { get; private set; } = null!;

    public UserStatus Status { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    public static Result<User> Register(Email email, PersonName name, PasswordHash passwordHash)
    {
        var user = new User(Guid.NewGuid(), email, name, passwordHash);

        user.RaiseDomainEvent(new UserRegisteredDomainEvent(user.Id));

        return Result.Success(user);
    }

    public Result VerifyEmail()
    {
        if (Status != UserStatus.Pending)
        {
            return Result.Failure(AlreadyVerified);
        }

        Status = UserStatus.Active;
        Touch();

        return Result.Success();
    }

    public Result ChangeEmail(Email email)
    {
        if (Status == UserStatus.Deactivated)
        {
            return Result.Failure(Deactivated);
        }

        Email = email;
        Touch();

        return Result.Success();
    }

    public Result ChangeName(PersonName name)
    {
        if (Status == UserStatus.Deactivated)
        {
            return Result.Failure(Deactivated);
        }

        Name = name;
        Touch();

        return Result.Success();
    }

    public Result Lock()
    {
        if (Status == UserStatus.Locked)
        {
            return Result.Failure(AlreadyLocked);
        }

        if (Status == UserStatus.Deactivated)
        {
            return Result.Failure(Deactivated);
        }

        Status = UserStatus.Locked;
        Touch();

        RaiseDomainEvent(new UserLockedDomainEvent(Id));

        return Result.Success();
    }

    public Result Unlock()
    {
        if (Status != UserStatus.Locked)
        {
            return Result.Failure(NotLocked);
        }

        Status = UserStatus.Active;
        Touch();

        return Result.Success();
    }

    public Result Deactivate()
    {
        if (Status == UserStatus.Deactivated)
        {
            return Result.Failure(AlreadyDeactivated);
        }

        Status = UserStatus.Deactivated;
        Touch();

        return Result.Success();
    }

    public Result AssignRole(Guid roleId)
    {
        if (Status == UserStatus.Deactivated)
        {
            return Result.Failure(Deactivated);
        }

        if (_roles.Any(role => role.RoleId == roleId))
        {
            return Result.Failure(RoleAlreadyAssigned);
        }

        _roles.Add(new UserRole(Id, roleId));
        Touch();

        return Result.Success();
    }

    /// <summary>
    /// A user must retain at least one role. A user with none is not a user with
    /// reduced access — it is a user no permission check can reason about, which
    /// is a state the system should not be able to reach.
    /// </summary>
    public Result RemoveRole(Guid roleId)
    {
        var assigned = _roles.FirstOrDefault(role => role.RoleId == roleId);

        if (assigned is null)
        {
            return Result.Failure(RoleNotAssigned);
        }

        if (_roles.Count == 1)
        {
            return Result.Failure(LastRoleCannotBeRemoved);
        }

        _roles.Remove(assigned);
        Touch();

        return Result.Success();
    }

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}