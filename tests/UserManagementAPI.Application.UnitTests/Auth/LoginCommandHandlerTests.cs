using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Auth;
using UserManagementAPI.Application.Auth.Commands.Login;
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

    private LoginCommandHandler Handler() => new(
        _userRepository,
        _passwordHasher,
        new TokenIssuer(_permissionLookup, _tokenService, _refreshTokens),
        _unitOfWork);

    [Fact]
    public async Task ReturnsATokenPair_AndStoresTheRefreshTokenHash_ForCorrectCredentials()
    {
        var user = KnownUser(TestUsers.Active());
        _passwordHasher.Verify(Password, user.PasswordHash.Value).Returns(true);
        _permissionLookup.GetPermissionCodesAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { "users.read" });
        var accessExpires = DateTime.UtcNow.AddMinutes(15);
        var refreshExpires = DateTime.UtcNow.AddDays(7);
        _tokenService.CreateAccessToken(user, Arg.Is<IReadOnlyCollection<string>>(codes => codes.Contains("users.read")))
            .Returns(new IssuedToken("jwt", accessExpires));
        _tokenService.CreateRefreshToken().Returns(new IssuedToken("raw-refresh", refreshExpires));
        _tokenService.HashRefreshToken("raw-refresh").Returns("hashed-refresh");

        var result = await Handler().HandleAsync(new LoginCommand(user.Email.Value, Password));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new AuthTokens("jwt", accessExpires, "raw-refresh", refreshExpires));
        _refreshTokens.Received(1).Add(Arg.Is<RefreshToken>(token =>
            token.UserId == user.Id && token.TokenHash == "hashed-refresh" && token.IsActive));
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
    public async Task RefusesADeactivatedUser_AfterTheCorrectPassword()
    {
        var user = KnownUser(TestUsers.Deactivated());
        _passwordHasher.Verify(Password, user.PasswordHash.Value).Returns(true);

        var result = await Handler().HandleAsync(new LoginCommand(user.Email.Value, Password));

        result.Error.Should().Be(User.AccountDeactivated);
    }

    private User KnownUser(User user)
    {
        _userRepository.GetByEmailAsync(Arg.Is<Email>(email => email == user.Email), Arg.Any<CancellationToken>())
            .Returns(user);

        return user;
    }
}