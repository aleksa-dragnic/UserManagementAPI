using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Auth;
using UserManagementAPI.Application.Auth.Commands.RefreshToken;
using UserManagementAPI.Application.UnitTests.Users;
using UserManagementAPI.Domain.Auth;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.UnitTests.Auth;

public sealed class RefreshTokenCommandHandlerTests
{
    private const string Presented = "raw-token";

    private const string PresentedHash = "hash-of-raw-token";

    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IPermissionLookup _permissionLookup = Substitute.For<IPermissionLookup>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public RefreshTokenCommandHandlerTests()
    {
        _tokenService.HashRefreshToken(Presented).Returns(PresentedHash);
        _tokenService.CreateAccessToken(Arg.Any<User>(), Arg.Any<IReadOnlyCollection<string>>())
            .Returns(new IssuedToken("new-jwt", DateTime.UtcNow.AddMinutes(15)));
        _tokenService.CreateRefreshToken().Returns(new IssuedToken("new-raw", DateTime.UtcNow.AddDays(7)));
        _tokenService.HashRefreshToken("new-raw").Returns("new-hash");
    }

    private RefreshTokenCommandHandler Handler() => new(
        _refreshTokens,
        _userRepository,
        _tokenService,
        new TokenIssuer(_permissionLookup, _tokenService, _refreshTokens),
        _unitOfWork);

    [Fact]
    public async Task RotatesTheToken_AndReturnsANewPair()
    {
        var user = TestUsers.Active();
        var stored = StoredToken(user);

        var result = await Handler().HandleAsync(new RefreshTokenCommand(Presented));

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new-jwt");
        result.Value.RefreshToken.Should().Be("new-raw");
        stored.IsRotated.Should().BeTrue();
        _refreshTokens.Received(1).Add(Arg.Is<RefreshToken>(token =>
            token.Id == stored.ReplacedById && token.TokenHash == "new-hash" && token.UserId == user.Id));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplayingAnExchangedToken_RevokesEverySessionForTheUser_AndFails()
    {
        var user = TestUsers.Active();
        var stored = StoredToken(user);
        stored.Rotate("someone-elses-hash", DateTime.UtcNow.AddDays(7));

        var result = await Handler().HandleAsync(new RefreshTokenCommand(Presented));

        result.Error.Should().Be(AuthErrors.RefreshTokenReused);
        await _refreshTokens.Received(1).RevokeAllActiveForUserAsync(user.Id, Arg.Any<CancellationToken>());
        _refreshTokens.DidNotReceive().Add(Arg.Any<RefreshToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnExpiredToken_Fails()
    {
        StoredToken(TestUsers.Active(), lifetime: TimeSpan.FromSeconds(-1));

        var result = await Handler().HandleAsync(new RefreshTokenCommand(Presented));

        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
        await _refreshTokens.DidNotReceive().RevokeAllActiveForUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ARevokedToken_Fails()
    {
        var stored = StoredToken(TestUsers.Active());
        stored.Revoke();

        var result = await Handler().HandleAsync(new RefreshTokenCommand(Presented));

        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task AnUnknownToken_Fails()
    {
        var result = await Handler().HandleAsync(new RefreshTokenCommand(Presented));

        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
        _tokenService.DidNotReceive().CreateRefreshToken();
    }

    [Fact]
    public async Task ALockedUser_CannotRefresh()
    {
        StoredToken(TestUsers.Locked());

        var result = await Handler().HandleAsync(new RefreshTokenCommand(Presented));

        result.Error.Should().Be(User.AccountLocked);
        _refreshTokens.DidNotReceive().Add(Arg.Any<RefreshToken>());
    }

    private RefreshToken StoredToken(User user, TimeSpan? lifetime = null)
    {
        var token = RefreshToken.Issue(user.Id, PresentedHash, DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromDays(7))).Value;

        _refreshTokens.GetByTokenHashAsync(PresentedHash, Arg.Any<CancellationToken>()).Returns(token);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        return token;
    }
}