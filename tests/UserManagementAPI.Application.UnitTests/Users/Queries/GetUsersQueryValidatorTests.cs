using FluentValidation.TestHelper;

using UserManagementAPI.Application.Users.Queries.GetUsers;

namespace UserManagementAPI.Application.UnitTests.Users.Queries;

public sealed class GetUsersQueryValidatorTests
{
    private readonly GetUsersQueryValidator _validator = new();

    [Theory]
    [InlineData("active")]
    [InlineData("LOCKED")]
    [InlineData(" Pending ")]
    public void AcceptsAKnownStatus_InAnyCase(string status)
    {
        _validator.TestValidate(new GetUsersQuery { Status = status }).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RejectsAnUnknownStatus()
    {
        _validator.TestValidate(new GetUsersQuery { Status = "banned" })
            .ShouldHaveValidationErrorFor(query => query.Status);
    }

    [Fact]
    public void AcceptsNoStatus()
    {
        _validator.TestValidate(new GetUsersQuery()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RejectsAnOverlongSearchTerm()
    {
        var term = new string('x', GetUsersQueryValidator.MaxSearchTermLength + 1);

        _validator.TestValidate(new GetUsersQuery { SearchTerm = term })
            .ShouldHaveValidationErrorFor(query => query.SearchTerm);
    }
}