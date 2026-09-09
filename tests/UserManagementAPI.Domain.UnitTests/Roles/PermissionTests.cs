using UserManagementAPI.Domain.Roles;

namespace UserManagementAPI.Domain.UnitTests.Roles;

public class PermissionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Fails_WhenCodeIsMissing(string? code)
    {
        Permission.Create(code).Error.Should().Be(Permission.CodeEmpty);
    }

    [Fact]
    public void Create_Fails_WhenCodeExceedsMaxLength()
    {
        Permission.Create(new string('a', Permission.MaxCodeLength + 1))
            .Error.Should().Be(Permission.CodeTooLong);
    }

    [Fact]
    public void Create_NormalisesTheCode()
    {
        var result = Permission.Create("  Users.Read  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("users.read");
    }
}