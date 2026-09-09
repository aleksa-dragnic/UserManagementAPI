namespace UserManagementAPI.Domain.UnitTests.Common;

public class ValueObjectTests
{
    [Fact]
    public void Equals_ReturnsTrue_WhenComponentsAreIdentical()
    {
        var first = new TestValueObject("alpha", 1);
        var second = new TestValueObject("alpha", 1);

        first.Equals(second).Should().BeTrue();
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenAnyComponentDiffers()
    {
        var first = new TestValueObject("alpha", 1);
        var second = new TestValueObject("alpha", 2);

        (first != second).Should().BeTrue();
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenTypesDifferButComponentsMatch()
    {
        var value = new TestValueObject("alpha", 1);
        var other = new OtherTestValueObject("alpha", 1);

        value.Equals(other).Should().BeFalse();
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenOtherIsNull()
    {
        var value = new TestValueObject("alpha", 1);

        value.Equals(null).Should().BeFalse();
    }
}