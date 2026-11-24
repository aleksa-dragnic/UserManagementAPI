using UserManagementAPI.Domain.Auth;

namespace UserManagementAPI.Domain.UnitTests.Auth;

public sealed class RefreshTokenTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static RefreshToken Issued(TimeSpan? lifetime = null) =>
        RefreshToken.Issue(UserId, "hash-1", TestClock.Now.Add(lifetime ?? TimeSpan.FromDays(7)), TestClock.Now).Value;

    [Fact]
    public void Issue_ProducesAnActiveToken()
    {
        var token = Issued();

        token.IsActiveAt(TestClock.Now).Should().BeTrue();
        token.UserId.Should().Be(UserId);
        token.TokenHash.Should().Be("hash-1");
        token.ReplacedById.Should().BeNull();
        token.RevokedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Issue_Fails_WithoutAHash()
    {
        RefreshToken.Issue(UserId, " ", TestClock.Now.AddDays(1), TestClock.Now).Error.Should().Be(RefreshToken.HashEmpty);
    }

    [Fact]
    public void Rotate_ReturnsANewActiveToken_AndRetiresTheOldOne()
    {
        var old = Issued();

        var replacement = old.Rotate("hash-2", TestClock.Now.AddDays(7), TestClock.Now);

        replacement.IsSuccess.Should().BeTrue();
        replacement.Value.IsActiveAt(TestClock.Now).Should().BeTrue();
        replacement.Value.UserId.Should().Be(UserId);
        old.ReplacedById.Should().Be(replacement.Value.Id);
        old.IsRotated.Should().BeTrue();
        old.IsRevoked.Should().BeFalse();
        old.IsActiveAt(TestClock.Now).Should().BeFalse();
    }

    [Fact]
    public void Rotate_Fails_OnATokenAlreadyRotated()
    {
        var old = Issued();
        old.Rotate("hash-2", TestClock.Now.AddDays(7), TestClock.Now);

        old.Rotate("hash-3", TestClock.Now.AddDays(7), TestClock.Now).Error.Should().Be(RefreshToken.NotActive);
    }

    [Fact]
    public void Rotate_Fails_OnARevokedToken()
    {
        var token = Issued();
        token.Revoke(TestClock.Now);

        token.Rotate("hash-2", TestClock.Now.AddDays(7), TestClock.Now).Error.Should().Be(RefreshToken.NotActive);
    }

    [Fact]
    public void AnExpiredToken_IsNotActive()
    {
        var token = Issued(TimeSpan.FromSeconds(-1));

        token.IsExpiredAt(TestClock.Now).Should().BeTrue();
        token.IsActiveAt(TestClock.Now).Should().BeFalse();
    }

    [Fact]
    public void ATokenIsExpired_FromItsExpiryInstantOn_AndNotATickBefore()
    {
        var token = Issued(TimeSpan.FromDays(7));
        var expiry = TestClock.Now.AddDays(7);

        token.IsExpiredAt(expiry.AddTicks(-1)).Should().BeFalse();
        token.IsActiveAt(expiry.AddTicks(-1)).Should().BeTrue();
        token.IsExpiredAt(expiry).Should().BeTrue();
        token.IsActiveAt(expiry).Should().BeFalse();
    }

    [Fact]
    public void IssueAndRevoke_RecordTheTimesTheyWereGiven()
    {
        var token = Issued();
        var revokedAt = TestClock.Now.AddHours(1);

        token.Revoke(revokedAt);

        token.CreatedAtUtc.Should().Be(TestClock.Now);
        token.RevokedAtUtc.Should().Be(revokedAt);
    }

    [Fact]
    public void Revoke_Twice_FailsTheSecondTime()
    {
        var token = Issued();

        token.Revoke(TestClock.Now).IsSuccess.Should().BeTrue();
        token.Revoke(TestClock.Now).Error.Should().Be(RefreshToken.AlreadyRevoked);
        token.IsActiveAt(TestClock.Now).Should().BeFalse();
    }
}