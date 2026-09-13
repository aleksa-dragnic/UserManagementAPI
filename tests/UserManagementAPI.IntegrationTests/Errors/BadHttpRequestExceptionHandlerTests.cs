using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

// NSubstitute is referenced by this project but not among its global usings,
// unlike Application.UnitTests. Imported here rather than added to the csproj:
// this is the only file in the project that needs it.
using NSubstitute;

using UserManagementAPI.Api.Errors;

namespace UserManagementAPI.IntegrationTests.Errors;

/// <summary>
/// Exercised directly rather than over HTTP, and deliberately so: the
/// functional suite runs on TestServer, which is not Kestrel and never raises
/// this exception, so a test that drove it through the stack would assert
/// nothing about the case that matters. What can be pinned down is that the
/// handler claims the exception and answers with the status the exception
/// carries instead of 500.
/// </summary>
public sealed class BadHttpRequestExceptionHandlerTests
{
    [Theory]
    [InlineData(413)]
    [InlineData(400)]
    public async Task Handles_a_bad_request_with_the_status_the_exception_carries(int status)
    {
        var problemDetails = Substitute.For<IProblemDetailsService>();
        problemDetails.TryWriteAsync(Arg.Any<ProblemDetailsContext>()).Returns(new ValueTask<bool>(true));

        var handler = new BadHttpRequestExceptionHandler(
            problemDetails,
            NullLogger<BadHttpRequestExceptionHandler>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/users";

        var handled = await handler.TryHandleAsync(
            context,
            new BadHttpRequestException("Request body too large.", status),
            CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(status);
    }

    [Fact]
    public async Task Leaves_every_other_exception_to_the_handler_behind_it()
    {
        var problemDetails = Substitute.For<IProblemDetailsService>();

        var handler = new BadHttpRequestExceptionHandler(
            problemDetails,
            NullLogger<BadHttpRequestExceptionHandler>.Instance);

        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("something else"),
            CancellationToken.None);

        handled.Should().BeFalse();
        await problemDetails.DidNotReceive().TryWriteAsync(Arg.Any<ProblemDetailsContext>());
    }
}