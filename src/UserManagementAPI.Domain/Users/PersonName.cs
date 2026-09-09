using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Domain.Users;

/// <summary>
/// A user's first and last name, trimmed. Both parts are required — a system
/// that allows a blank half ends up rendering "Ana " everywhere.
/// </summary>
public sealed class PersonName : ValueObject
{
    public const int MaxPartLength = 100;

    public static readonly Error FirstEmpty = new("PersonName.FirstEmpty", "First name must be provided.");

    public static readonly Error LastEmpty = new("PersonName.LastEmpty", "Last name must be provided.");

    public static readonly Error TooLong = new(
        "PersonName.TooLong",
        $"Neither name part may exceed {MaxPartLength} characters.");

    private PersonName(string first, string last)
    {
        First = first;
        Last = last;
    }

    public string First { get; }

    public string Last { get; }

    public string FullName => $"{First} {Last}";

    public static Result<PersonName> Create(string? first, string? last)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return Result.Failure<PersonName>(FirstEmpty);
        }

        if (string.IsNullOrWhiteSpace(last))
        {
            return Result.Failure<PersonName>(LastEmpty);
        }

        var trimmedFirst = first.Trim();
        var trimmedLast = last.Trim();

        if (trimmedFirst.Length > MaxPartLength || trimmedLast.Length > MaxPartLength)
        {
            return Result.Failure<PersonName>(TooLong);
        }

        return Result.Success(new PersonName(trimmedFirst, trimmedLast));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return First;
        yield return Last;
    }

    public override string ToString() => FullName;
}