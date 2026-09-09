using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Domain.UnitTests.Common;

public class EnumerationTests
{
    [Fact]
    public void GetAll_ReturnsEveryDeclaredInstance()
    {
        var all = Enumeration.GetAll<TestEnumeration>();

        all.Should().HaveCount(2);
        all.Should().Contain([TestEnumeration.First, TestEnumeration.Second]);
    }

    [Fact]
    public void FromId_ReturnsTheDeclaredInstance()
    {
        var result = Enumeration.FromId<TestEnumeration>(2);

        result.Should().BeSameAs(TestEnumeration.Second);
    }

    [Fact]
    public void FromId_Throws_WhenNoInstanceHasTheId()
    {
        var act = () => Enumeration.FromId<TestEnumeration>(99);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromName_ReturnsTheDeclaredInstance_IgnoringCase()
    {
        var result = Enumeration.FromName<TestEnumeration>("first");

        result.Should().BeSameAs(TestEnumeration.First);
    }

    [Fact]
    public void FromName_Throws_WhenNoInstanceHasTheName()
    {
        var act = () => Enumeration.FromName<TestEnumeration>("Third");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CompareTo_OrdersById()
    {
        var ordered = new[] { TestEnumeration.Second, TestEnumeration.First }.Order().ToList();

        ordered.Should().ContainInOrder(TestEnumeration.First, TestEnumeration.Second);
    }

    [Fact]
    public void Equals_ReturnsTrue_ForTheSameInstance()
    {
        (TestEnumeration.First == Enumeration.FromId<TestEnumeration>(1)).Should().BeTrue();
        (TestEnumeration.First != TestEnumeration.Second).Should().BeTrue();
    }
}