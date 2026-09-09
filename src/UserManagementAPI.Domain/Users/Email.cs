using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Domain.Users;

/// <summary>
/// A user's email address, normalised to lowercase.
/// </summary>
public sealed class Email : ValueObject
{
    public const int MaxLength = 256;

    public static readonly Error Empty = new("Email.Empty", "Email must be provided.");

    public static readonly Error TooLong = new(
        "Email.TooLong",
        $"Email must not exceed {MaxLength} characters.");

    public static readonly Error InvalidFormat = new(
        "Email.InvalidFormat",
        "Email is not in a recognisable format.");

    private Email(string value) => Value = value;

    public string Value { get; }

    /// <summary>
    /// Validates structure only: one '@' with text on either side. Deliberately
    /// not an RFC 5322 regex — that regex rejects addresses that work and accepts
    /// addresses that do not deliver. What proves an address is real is a
    /// verification email, which is where the actual check belongs.
    /// </summary>
    public static Result<Email> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<Email>(Empty);
        }

        var normalised = value.Trim().ToLowerInvariant();

        if (normalised.Length > MaxLength)
        {
            return Result.Failure<Email>(TooLong);
        }

        var parts = normalised.Split('@');

        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            return Result.Failure<Email>(InvalidFormat);
        }

        return Result.Success(new Email(normalised));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}