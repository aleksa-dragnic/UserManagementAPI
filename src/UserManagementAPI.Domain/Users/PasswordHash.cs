using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Domain.Users;

/// <summary>
/// The stored hash of a password. No hashing happens here — the algorithm is an
/// Infrastructure concern (M4). This type exists so a raw password string can
/// never be assigned where a hash is expected: the compiler catches the mistake
/// that would otherwise write a plaintext password to the users table.
/// </summary>
public sealed class PasswordHash : ValueObject
{
    public static readonly Error Empty = new("PasswordHash.Empty", "Password hash must be provided.");

    private PasswordHash(string value) => Value = value;

    public string Value { get; }

    public static Result<PasswordHash> Create(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Result.Failure<PasswordHash>(Empty)
            : Result.Success(new PasswordHash(value));

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}