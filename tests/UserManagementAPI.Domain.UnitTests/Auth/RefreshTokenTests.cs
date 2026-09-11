using UserManagementAPI.Domain.Auth;

namespace UserManagementAPI.Domain.UnitTests.Auth;

public sealed class RefreshTokenTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static RefreshToken Issued(TimeSpan? lifetime = null) =>
        RefreshToken.Issue(UserId, "hash-1", DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromDays(7))).Value;

    [Fact]
    public void Issue_ProducesAnActiveToken()
    {
        var token = Issued();

        token.IsActive.Should().BeTrue();
        token.UserId.Should().Be(UserId);
        token.TokenHash.Should().Be("hash-1");
        token.ReplacedById.Should().BeNull();
        token.RevokedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Issue_Fails_WithoutAHash()
    {
        RefreshToken.Issue(UserId, " ", DateTime.UtcNow.AddDays(1)).Error.Should().Be(RefreshToken.HashEmpty);
    }

    [Fact]
    public void Rotate_ReturnsANewActiveToken_AndRetiresTheOldOne()
    {
        var old = Issued();

        var replacement = old.Rotate("hash-2", DateTime.UtcNow.AddDays(7));

        replacement.IsSuccess.Should().BeTrue();
        replacement.Value.IsActive.Should().BeTrue();
        replacement.Value.UserId.Should().Be(UserId);
        old.ReplacedById.Should().Be(replacement.Value.Id);
        old.IsRotated.Should().BeTrue();
        old.IsRevoked.Should().BeFalse();
        old.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Rotate_Fails_OnATokenAlreadyRotated()
    {
        var old = Issued();
        old.Rotate("hash-2", DateTime.UtcNow.AddDays(7));

        old.Rotate("hash-3", DateTime.UtcNow.AddDays(7)).Error.Should().Be(RefreshToken.NotActive);
    }

    [Fact]
    public void Rotate_Fails_OnARevokedToken()
    {
        var token = Issued();
        token.Revoke();

        token.Rotate("hash-2", DateTime.UtcNow.AddDays(7)).Error.Should().Be(RefreshToken.NotActive);
    }

    [Fact]
    public void AnExpiredToken_IsNotActive()
    {
        var token = Issued(TimeSpan.FromSeconds(-1));

        token.IsExpired.Should().BeTrue();
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Revoke_Twice_FailsTheSecondTime()
    {
        var token = Issued();

        token.Revoke().IsSuccess.Should().BeTrue();
        token.Revoke().Error.Should().Be(RefreshToken.AlreadyRevoked);
        token.IsActive.Should().BeFalse();
    }
}