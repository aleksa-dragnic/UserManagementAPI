using System.Reflection;

using UserManagementAPI.Api.Errors;
using UserManagementAPI.Api.Extensions;
using UserManagementAPI.Application.Auth;
using UserManagementAPI.Application.Common;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.IntegrationTests.Errors;

/// <summary>
/// The status every error code is answered with is part of the contract: a
/// client branches on it. The table is the contract written down, and the
/// reflection test makes a new error fail the build until it is added here.
/// </summary>
public sealed class ErrorMappingTests
{
    private static readonly Dictionary<string, int> Expected = new()
    {
        ["General.NullValue"] = 400,
        ["Request.InvalidIfMatch"] = 400,
        // Answered 413 by its handler, which sets the status itself; its type is Failure.
        ["Request.ContentTooLarge"] = 400,
        ["Request.Malformed"] = 400,
        ["Concurrency.Conflict"] = 409,
        [ValidationError.GeneralCode] = 422,

        ["Auth.InvalidCredentials"] = 401,
        ["Auth.InvalidRefreshToken"] = 401,
        ["Auth.RefreshTokenReused"] = 401,
        ["Auth.AccountLocked"] = 401,
        ["Auth.AccountDeactivated"] = 401,

        ["RefreshToken.HashEmpty"] = 400,
        ["RefreshToken.NotActive"] = 400,
        ["RefreshToken.AlreadyRevoked"] = 409,

        ["Permission.CodeEmpty"] = 400,
        ["Permission.CodeTooLong"] = 400,

        ["Role.NameEmpty"] = 400,
        ["Role.NameTooLong"] = 400,
        ["Role.PermissionAlreadyAdded"] = 409,
        ["Role.PermissionNotHeld"] = 400,
        ["Role.NotFound"] = 404,

        ["Email.Empty"] = 400,
        ["Email.TooLong"] = 400,
        ["Email.InvalidFormat"] = 400,
        ["PasswordHash.Empty"] = 400,
        ["PersonName.FirstEmpty"] = 400,
        ["PersonName.LastEmpty"] = 400,
        ["PersonName.TooLong"] = 400,

        ["User.AlreadyVerified"] = 409,
        ["User.Deactivated"] = 400,
        ["User.AlreadyLocked"] = 409,
        ["User.CannotLockSelf"] = 400,
        ["User.NotLocked"] = 400,
        ["User.AlreadyDeactivated"] = 409,
        ["User.NotFound"] = 404,
        ["User.EmailNotUnique"] = 409,
        ["User.RoleAlreadyAssigned"] = 409,
        ["User.LastRoleCannotBeRemoved"] = 400,
        ["User.InitialRoleRequired"] = 400,
        ["User.VersionMismatch"] = 412,
        ["User.RoleNotAssigned"] = 400
    };

    public static TheoryData<string> Codes() => [.. Expected.Keys];

    [Theory]
    [MemberData(nameof(Codes))]
    public void EveryErrorCode_KeepsItsStatus(string code)
    {
        var error = DeclaredErrors().Should().ContainSingle(candidate => candidate.Code == code).Subject;

        ErrorMapping.StatusCodeFor(error).Should().Be(Expected[code]);
    }

    [Fact]
    public void EveryDeclaredError_IsInTheTable()
    {
        DeclaredErrors().Select(error => error.Code).Should().BeEquivalentTo(Expected.Keys);
    }

    /// <summary>
    /// Every public static Error in the assemblies that declare them, the
    /// validation error included (it is constructed per request, not declared).
    /// </summary>
    private static IEnumerable<Error> DeclaredErrors() =>
        new[] { typeof(User).Assembly, typeof(AuthErrors).Assembly, typeof(IfMatchHeader).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.FieldType == typeof(Error))
            .Select(field => (Error)field.GetValue(null)!)
            .Where(error => error != Error.None)
            .Append(new ValidationError([]));
}
