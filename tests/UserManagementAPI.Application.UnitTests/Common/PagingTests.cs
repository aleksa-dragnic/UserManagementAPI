using UserManagementAPI.Application.Common;
using UserManagementAPI.Application.Users.Queries.GetUsers;

namespace UserManagementAPI.Application.UnitTests.Common;

public sealed class PagingTests
{
    [Theory]
    [InlineData(51)]
    [InlineData(1000)]
    public void PageSize_IsClampedToTheMaximum(int requested)
    {
        var query = new GetUsersQuery { PageSize = requested };

        query.PageSize.Should().Be(RequestParameters.MaxPageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void PageSize_BelowOne_FallsBackToTheDefault(int requested)
    {
        var query = new GetUsersQuery { PageSize = requested };

        query.PageSize.Should().Be(RequestParameters.DefaultPageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PageNumber_BelowOne_BecomesOne(int requested)
    {
        var query = new GetUsersQuery { PageNumber = requested };

        query.PageNumber.Should().Be(1);
    }

    [Fact]
    public void MetaData_DescribesThePageAndItsNeighbours()
    {
        var metaData = MetaData.Create(pageNumber: 2, pageSize: 10, totalCount: 25);

        metaData.TotalPages.Should().Be(3);
        metaData.HasPrevious.Should().BeTrue();
        metaData.HasNext.Should().BeTrue();
    }

    [Fact]
    public void MetaData_ForAnEmptyCollection_HasNoPagesAndNoNeighbours()
    {
        var metaData = MetaData.Create(pageNumber: 1, pageSize: 10, totalCount: 0);

        metaData.TotalPages.Should().Be(0);
        metaData.HasPrevious.Should().BeFalse();
        metaData.HasNext.Should().BeFalse();
    }
}