using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.Auth;

/// <summary>
/// Authentication failures. The "Auth." family maps to 401. There is one error
/// for a wrong email and a wrong password because a distinguishable response
/// tells an attacker which addresses are registered.
/// </summary>
public static class AuthErrors
{
    public static readonly Error InvalidCredentials = new(
        "Auth.InvalidCredentials",
        "The email or password is incorrect.");
}