namespace UserManagementAPI.Domain.UnitTests.Common;

public class EntityTests
{
    [Fact]
    public void Equals_ReturnsTrue_WhenSameTypeAndSameId()
    {
        var id = Guid.NewGuid();

        var first = new TestEntity(id);
        var second = new TestEntity(id);

        first.Equals(second).Should().BeTrue();
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenDifferentTypesShareAnId()
    {
        var id = Guid.NewGuid();

        var entity = new TestEntity(id);
        var other = new OtherTestEntity(id);

        entity.Equals(other).Should().BeFalse();
        entity.GetHashCode().Should().NotBe(other.GetHashCode());
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenIdsDiffer()
    {
        var first = new TestEntity(Guid.NewGuid());
        var second = new TestEntity(Guid.NewGuid());

        (first != second).Should().BeTrue();
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenOtherIsNull()
    {
        var entity = new TestEntity(Guid.NewGuid());

        entity.Equals(null).Should().BeFalse();
        (entity == null).Should().BeFalse();
    }
}