using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Auth.Commands.Logout;
using UserManagementAPI.Domain.Auth;

namespace UserManagementAPI.Application.UnitTests.Auth;

public sealed class LogoutCommandHandlerTests
{
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private LogoutCommandHandler Handler() => new(_refreshTokens, _tokenService, _unitOfWork);

    [Fact]
    public async Task RevokesThePresentedToken_AndSaves()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", DateTime.UtcNow.AddDays(7)).Value;
        _tokenService.HashRefreshToken("raw").Returns("hash");
        _refreshTokens.GetByTokenHashAsync("hash", Arg.Any<CancellationToken>()).Returns(token);

        var result = await Handler().HandleAsync(new LogoutCommand("raw"));

        result.IsSuccess.Should().BeTrue();
        token.IsRevoked.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsIdempotent_ForAnUnknownOrAlreadyRevokedToken()
    {
        var revoked = RefreshToken.Issue(Guid.NewGuid(), "revoked-hash", DateTime.UtcNow.AddDays(7)).Value;
        revoked.Revoke();
        _tokenService.HashRefreshToken("revoked").Returns("revoked-hash");
        _tokenService.HashRefreshToken("unknown").Returns("unknown-hash");
        _refreshTokens.GetByTokenHashAsync("revoked-hash", Arg.Any<CancellationToken>()).Returns(revoked);

        (await Handler().HandleAsync(new LogoutCommand("revoked"))).IsSuccess.Should().BeTrue();
        (await Handler().HandleAsync(new LogoutCommand("unknown"))).IsSuccess.Should().BeTrue();

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}