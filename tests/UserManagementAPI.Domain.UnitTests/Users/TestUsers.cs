using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Domain.UnitTests.Users;

/// <summary>
/// Builds users in each lifecycle state through the aggregate's own methods.
/// Reaching a state any other way would test a user the application can never
/// produce.
/// </summary>
internal static class TestUsers
{
    // Fully qualified: inside this class the simple name "Email" is the method
    // below, and "Users.Email" would resolve to this test namespace.
    public static Email Email(string value = "ana.petrovic@example.com") =>
        UserManagementAPI.Domain.Users.Email.Create(value).Value;

    public static PersonName Name(string first = "Ana", string last = "Petrović") =>
        PersonName.Create(first, last).Value;

    public static PasswordHash Hash(string value = "argon2id$hash") =>
        PasswordHash.Create(value).Value;

    public static User Pending() => User.Register(Email(), Name(), Hash()).Value;

    public static User Active()
    {
        var user = Pending();
        user.VerifyEmail();
        return user;
    }

    public static User Locked()
    {
        var user = Active();
        user.Lock();
        return user;
    }

    public static User Deactivated()
    {
        var user = Active();
        user.Deactivate();
        return user;
    }
}