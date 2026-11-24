using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Auth;
using UserManagementAPI.Application.Auth.Commands.Login;
using UserManagementAPI.Application.Common;
using UserManagementAPI.Application.UnitTests.Users;
using UserManagementAPI.Domain.Auth;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.UnitTests.Auth;

public sealed class LoginCommandHandlerTests
{
    private const string Password = "correct horse battery staple";

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IPermissionLookup _permissionLookup = Substitute.For<IPermissionLookup>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILoginThrottle _throttle = Substitute.For<ILoginThrottle>();

    private readonly FakeTimeProvider _clock = new(TestClock.Now);

    private LoginCommandHandler Handler() => new(
        _userRepository,
        _passwordHasher,
        new TokenIssuer(_permissionLookup, _tokenService, _refreshTokens),
        _throttle,
        _unitOfWork,
        _clock);

    [Fact]
    public async Task ReturnsATokenPair_AndStoresTheRefreshTokenHash_ForCorrectCredentials()
    {
        var user = KnownUser(TestUsers.Active());
        _passwordHasher.Verify(Password, user.PasswordHash.Value).Returns(true);
        _permissionLookup.GetPermissionCodesAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { "users.read" });
        var accessExpires = TestClock.Now.AddMinutes(15);
        var refreshExpires = TestClock.Now.AddDays(7);
        _tokenService.CreateAccessToken(user, Arg.Is<IReadOnlyCollection<string>>(codes => codes.Contains("users.read")))
            .Returns(new IssuedToken("jwt", accessExpires));
        _tokenService.CreateRefreshToken().Returns(new IssuedToken("raw-refresh", refreshExpires));
        _tokenService.HashRefreshToken("raw-refresh").Returns("hashed-refresh");

        var result = await Handler().HandleAsync(new LoginCommand(user.Email.Value, Password));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new AuthTokens("jwt", accessExpires, "raw-refresh", refreshExpires));
        _refreshTokens.Received(1).Add(Arg.Is<RefreshToken>(token =>
            token.UserId == user.Id && token.TokenHash == "hashed-refresh" && token.IsActiveAt(TestClock.Now)));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsTheSameError_ForAnUnknownEmailAndForAWrongPassword()
    {
        var user = KnownUser(TestUsers.Active());
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var unknownEmail = await Handler().HandleAsync(new LoginCommand("nobody@example.com", Password));
        var wrongPassword = await Handler().HandleAsync(new LoginCommand(user.Email.Value, "wrong"));

        unknownEmail.Error.Should().Be(AuthErrors.InvalidCredentials);
        wrongPassword.Error.Should().Be(AuthErrors.InvalidCredentials);
        _tokenService.DidNotReceiveWithAnyArgs().CreateAccessToken(default!, default!);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifiesAPassword_EvenWhenTheEmailIsUnknown()
    {
        await Handler().HandleAsync(new LoginCommand("nobody@example.com", Password));

        // Same cost as a real check, so response time does not reveal whether
        // the address is registered.
        _passwordHasher.Received(1).Verify(Password, Arg.Any<string>());
    }

    [Fact]
    public async Task RefusesALockedUser_AfterTheCorrectPassword()
    {
        var user = KnownUser(TestUsers.Locked());
        _passwordHasher.Verify(Password, user.PasswordHash.Value).Returns(true);

        var result = await Handler().HandleAsync(new LoginCommand(user.Email.Value, Password));

        result.Error.Should().Be(User.AccountLocked);
        _tokenService.DidNotReceiveWithAnyArgs().CreateAccessToken(default!, default!);
    }

    [Fact]
    public async Task ALockedUser_WithAWrongPassword_GetsTheSameErrorAsEveryoneElse()
    {
        var user = KnownUser(TestUsers.Locked());
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var result = await Handler().HandleAsync(new LoginCommand(user.Email.Value, "wrong"));

        // The lock is checked after the password. Checked first, it would tell
        // anyone without the password that the address belongs to a locked
        // account.
        result.Error.Should().Be(AuthErrors.InvalidCredentials);
        _tokenService.DidNotReceiveWithAnyArgs().CreateAccessToken(default!, default!);
    }

    [Fact]
    public async Task RefusesADeactivatedUser_AfterTheCorrectPassword()
    {
        var user = KnownUser(TestUsers.Deactivated());
        _passwordHasher.Verify(Password, user.PasswordHash.Value).Returns(true);

        var result = await Handler().HandleAsync(new LoginCommand(user.Email.Value, Password));

        result.Error.Should().Be(User.AccountDeactivated);
    }

    [Fact]
    public async Task WhileTheEmailIsThrottled_RefusesWith429_WithoutTryingThePassword()
    {
        var user = KnownUser(TestUsers.Active());
        _passwordHasher.Verify(Password, user.PasswordHash.Value).Returns(true);
        _throttle.BlockedForAsync(user.Email.Value, Arg.Any<CancellationToken>())
            .Returns(TimeSpan.FromSeconds(4));

        var result = await Handler().HandleAsync(new LoginCommand(user.Email.Value, Password));

        // The right password is refused the same way: the refusal says nothing about it.
        var refused = result.Error.Should().BeOfType<RateLimitedError>().Subject;
        refused.Code.Should().Be(AuthErrors.TooManyFailedAttemptsCode);
        refused.RetryAfter.Should().Be(TimeSpan.FromSeconds(4));
        _passwordHasher.DidNotReceiveWithAnyArgs().Verify(default!, default!);
        _tokenService.DidNotReceiveWithAnyArgs().CreateAccessToken(default!, default!);
    }

    [Fact]
    public async Task AWrongPassword_AndAnUnknownEmail_EachCountAFailure()
    {
        var user = KnownUser(TestUsers.Active());
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await Handler().HandleAsync(new LoginCommand(user.Email.Value, "wrong"));
        await Handler().HandleAsync(new LoginCommand("nobody@example.com", Password));

        await _throttle.Received(1).RecordFailureAsync(user.Email.Value, Arg.Any<CancellationToken>());
        await _throttle.Received(1).RecordFailureAsync("nobody@example.com", Arg.Any<CancellationToken>());
        await _throttle.DidNotReceiveWithAnyArgs().ResetAsync(default!, default);
    }

    [Fact]
    public async Task ASuccessfulLogin_ForgetsTheFailures()
    {
        var user = KnownUser(TestUsers.Active());
        _passwordHasher.Verify(Password, user.PasswordHash.Value).Returns(true);
        _tokenService.CreateAccessToken(user, Arg.Any<IReadOnlyCollection<string>>())
            .Returns(new IssuedToken("jwt", TestClock.Now.AddMinutes(15)));
        _tokenService.CreateRefreshToken().Returns(new IssuedToken("raw", TestClock.Now.AddDays(7)));
        _tokenService.HashRefreshToken("raw").Returns("hashed");

        var result = await Handler().HandleAsync(new LoginCommand(user.Email.Value, Password));

        result.IsSuccess.Should().BeTrue();
        await _throttle.Received(1).ResetAsync(user.Email.Value, Arg.Any<CancellationToken>());
        await _throttle.DidNotReceiveWithAnyArgs().RecordFailureAsync(default!, default);
    }

    [Fact]
    public async Task ALockedUser_WithTheRightPassword_NeitherCountsNorForgets()
    {
        var user = KnownUser(TestUsers.Locked());
        _passwordHasher.Verify(Password, user.PasswordHash.Value).Returns(true);

        await Handler().HandleAsync(new LoginCommand(user.Email.Value, Password));

        // The password was right, so it is no guess; the account is not usable,
        // so it is no success either.
        await _throttle.DidNotReceiveWithAnyArgs().RecordFailureAsync(default!, default);
        await _throttle.DidNotReceiveWithAnyArgs().ResetAsync(default!, default);
    }

    private User KnownUser(User user)
    {
        _userRepository.GetByEmailAsync(Arg.Is<Email>(email => email == user.Email), Arg.Any<CancellationToken>())
            .Returns(user);

        return user;
    }
}