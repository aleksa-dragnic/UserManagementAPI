using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Auth.Commands.Login;

/// <summary>
/// Verifies credentials and issues an access token. Two properties matter more
/// than the happy path: the response for an unknown email and for a wrong
/// password is the same error, and it takes the same time — a password is
/// verified either way, against a decoy hash when there is no user. A
/// distinguishable response, in content or in timing, is a user-enumeration hole.
/// </summary>
public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IPermissionLookup permissionLookup,
    ITokenService tokenService) : ICommandHandler<LoginCommand, AuthTokens>
{
    private const string DecoyPassword = "decoy-password-so-unknown-emails-cost-a-verification-too";

    // Hashed once per process, lazily, with the real hasher and its real
    // parameters, so verifying against it costs what a real verification costs.
    private static string? s_decoyHash;

    public async Task<Result<AuthTokens>> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var email = Email.Create(command.Email);

        var user = email.IsSuccess
            ? await userRepository.GetByEmailAsync(email.Value, cancellationToken)
            : null;

        var storedHash = user?.PasswordHash.Value ?? DecoyHash();
        var passwordMatches = passwordHasher.Verify(command.Password, storedHash);

        if (user is null || !passwordMatches)
        {
            return Result.Failure<AuthTokens>(AuthErrors.InvalidCredentials);
        }

        var canLogIn = user.EnsureCanLogIn();

        if (canLogIn.IsFailure)
        {
            return Result.Failure<AuthTokens>(canLogIn.Error);
        }

        var permissions = await permissionLookup.GetPermissionCodesAsync(user.Id, cancellationToken);

        var accessToken = tokenService.CreateAccessToken(user, permissions);

        return Result.Success(new AuthTokens(accessToken.Value, accessToken.ExpiresAtUtc));
    }

    private string DecoyHash() => s_decoyHash ??= passwordHasher.Hash(DecoyPassword);
}