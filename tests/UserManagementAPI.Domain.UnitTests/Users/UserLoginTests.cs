using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Domain.UnitTests.Users;

public sealed class UserLoginTests
{
    [Fact]
    public void EnsureCanLogIn_Succeeds_ForAPendingUser() =>
        TestUsers.Pending().EnsureCanLogIn().IsSuccess.Should().BeTrue();

    [Fact]
    public void EnsureCanLogIn_Succeeds_ForAnActiveUser() =>
        TestUsers.Active().EnsureCanLogIn().IsSuccess.Should().BeTrue();

    [Fact]
    public void EnsureCanLogIn_Fails_ForALockedUser() =>
        TestUsers.Locked().EnsureCanLogIn().Error.Should().Be(User.AccountLocked);

    [Fact]
    public void EnsureCanLogIn_Fails_ForADeactivatedUser() =>
        TestUsers.Deactivated().EnsureCanLogIn().Error.Should().Be(User.AccountDeactivated);
}