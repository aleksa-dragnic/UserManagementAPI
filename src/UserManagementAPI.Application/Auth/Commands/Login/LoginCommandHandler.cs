using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Auth.Commands.Login;

/// <summary>
/// Verifies credentials and issues a token pair. Two properties matter more
/// than the happy path: the response for an unknown email and for a wrong
/// password is the same error, and it takes the same time — a password is
/// verified either way, against a decoy hash when there is no user. A
/// distinguishable response, in content or in timing, is a user-enumeration hole.
///
/// Before any of that, the login throttle (ADR 0024): after enough wrong
/// passwords in a row for one email, sign-in for it is refused for a while,
/// whatever password comes next.
/// </summary>
public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    TokenIssuer tokenIssuer,
    ILoginThrottle loginThrottle,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<LoginCommand, AuthTokens>
{
    private const string DecoyPassword = "decoy-password-so-unknown-emails-cost-a-verification-too";

    // Hashed once per process, lazily, with the real hasher and its real
    // parameters, so verifying against it costs what a real verification costs.
    private static string? s_decoyHash;

    public async Task<Result<AuthTokens>> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        // Checked before the password is: while it lasts, no password is tried.
        if (await loginThrottle.BlockedForAsync(command.Email, cancellationToken) is { } wait)
        {
            AuthMetrics.LoginRefused(AuthErrors.TooManyFailedAttemptsCode);
            return Result.Failure<AuthTokens>(AuthErrors.TooManyFailedAttempts(wait));
        }

        var email = Email.Create(command.Email);

        var user = email.IsSuccess
            ? await userRepository.GetByEmailAsync(email.Value, cancellationToken)
            : null;

        var storedHash = user?.PasswordHash.Value ?? DecoyHash();
        var passwordMatches = passwordHasher.Verify(command.Password, storedHash);

        if (user is null || !passwordMatches)
        {
            await loginThrottle.RecordFailureAsync(command.Email, cancellationToken);
            AuthMetrics.LoginRefused(AuthErrors.InvalidCredentials.Code);
            return Result.Failure<AuthTokens>(AuthErrors.InvalidCredentials);
        }

        var canLogIn = user.EnsureCanLogIn();

        if (canLogIn.IsFailure)
        {
            AuthMetrics.LoginRefused(canLogIn.Error.Code);
            return Result.Failure<AuthTokens>(canLogIn.Error);
        }

        var tokens = await tokenIssuer.IssueAsync(user, rotating: null, utcNow, cancellationToken);

        if (tokens.IsFailure)
        {
            return tokens;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await loginThrottle.ResetAsync(command.Email, cancellationToken);
        AuthMetrics.LoginSucceeded();

        return tokens;
    }

    private string DecoyHash() => s_decoyHash ??= passwordHasher.Hash(DecoyPassword);
}