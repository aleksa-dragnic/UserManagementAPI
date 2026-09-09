using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Domain.Roles;

/// <summary>
/// A named capability such as "users.read". The code is the unit of
/// authorization in M4 — policies check codes, never role names, so who may do
/// a thing becomes data rather than a redeploy.
/// </summary>
public sealed class Permission : Entity
{
    public const int MaxCodeLength = 100;

    public static readonly Error CodeEmpty = new("Permission.CodeEmpty", "Permission code must be provided.");

    public static readonly Error CodeTooLong = new(
        "Permission.CodeTooLong",
        $"Permission code must not exceed {MaxCodeLength} characters.");

    private Permission(Guid id, string code)
        : base(id) => Code = code;

    /// <summary>Required by EF Core.</summary>
    private Permission()
    {
    }

    public string Code { get; private set; } = null!;

    public static Result<Permission> Create(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure<Permission>(CodeEmpty);
        }

        var normalised = code.Trim().ToLowerInvariant();

        if (normalised.Length > MaxCodeLength)
        {
            return Result.Failure<Permission>(CodeTooLong);
        }

        return Result.Success(new Permission(Guid.NewGuid(), normalised));
    }

    public override string ToString() => Code;
}