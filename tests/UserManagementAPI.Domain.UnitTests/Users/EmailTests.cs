using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Domain.UnitTests.Users;

public class EmailTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Fails_WhenValueIsMissing(string? value)
    {
        var result = Email.Create(value);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Email.Empty);
    }

    [Theory]
    [InlineData("no-at-sign.example.com")]
    [InlineData("@example.com")]
    [InlineData("ana@")]
    [InlineData("ana@two@example.com")]
    public void Create_Fails_WhenStructureIsInvalid(string value)
    {
        var result = Email.Create(value);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Email.InvalidFormat);
    }

    [Fact]
    public void Create_Fails_WhenValueExceedsMaxLength()
    {
        var local = new string('a', Email.MaxLength);

        var result = Email.Create($"{local}@example.com");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Email.TooLong);
    }

    [Fact]
    public void Create_NormalisesCaseAndTrimsWhitespace()
    {
        var result = Email.Create("  Ana.Petrovic@Example.COM  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("ana.petrovic@example.com");
    }

    [Fact]
    public void Create_ProducesEqualInstances_ForAddressesDifferingOnlyInCase()
    {
        var first = Email.Create("ana@example.com").Value;
        var second = Email.Create("ANA@EXAMPLE.COM").Value;

        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }
}