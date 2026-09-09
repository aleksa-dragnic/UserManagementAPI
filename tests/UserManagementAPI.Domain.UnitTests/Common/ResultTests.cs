using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Domain.UnitTests.Common;

public class ResultTests
{
    private static readonly Error TestError = new("Test.Failure", "Something went wrong.");

    [Fact]
    public void Success_CarriesNoError()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_CarriesTheError()
    {
        var result = Result.Failure(TestError);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TestError);
    }

    [Fact]
    public void Failure_Throws_WhenTheErrorIsNone()
    {
        var act = () => Result.Failure(Error.None);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Value_ReturnsTheValue_OnSuccess()
    {
        var result = Result.Success("payload");

        result.Value.Should().Be("payload");
    }

    [Fact]
    public void Value_Throws_OnFailure()
    {
        var result = Result.Failure<string>(TestError);

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ImplicitConversion_ProducesSuccess_ForANonNullValue()
    {
        Result<string> result = "payload";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("payload");
    }

    [Fact]
    public void ImplicitConversion_ProducesFailure_ForNull()
    {
        Result<string> result = (string?)null;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NullValue);
    }
}