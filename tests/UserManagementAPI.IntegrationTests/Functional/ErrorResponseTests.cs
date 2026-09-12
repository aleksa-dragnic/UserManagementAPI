using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// What a client sees when a request goes wrong: problem details with the right
/// status every time, and — for a failure nobody anticipated — a 500 that
/// carries a traceId and no stack trace. That last one is the test that proves
/// GlobalExceptionHandler does what M0 claimed.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ErrorResponseTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private ApiFactory _factory = null!;

    private HttpClient _administrator = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture);
        await _factory.ResetAndSeedAsync();
        _administrator = await _factory.CreateAdministratorClientAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task AnInvalidField_Is422_WithTheFieldNamed()
    {
        var response = await _administrator.PostAsJsonAsync(
            "/api/v1/users",
            new RegisterUserRequest("not-an-email", "Ana", "Petrović", "Passw0rd-long-enough"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").EnumerateObject()
            .Select(property => property.Name).Should().Contain("Email");
    }

    [Fact]
    public async Task ADuplicateEmail_Is409()
    {
        await _administrator.RegisterUserAsync("duplicate@example.com", "Duplicate-Passw0rd!");

        var response = await _administrator.PostAsJsonAsync(
            "/api/v1/users",
            new RegisterUserRequest("duplicate@example.com", "Ana", "Petrović", "Passw0rd-long-enough"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.ErrorCodeAsync()).Should().Be(User.EmailNotUnique.Code);
    }

    [Fact]
    public async Task AnUnknownUser_Is404()
    {
        var response = await _administrator.PostAsync($"/api/v1/users/{Guid.NewGuid()}/lock", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.ErrorCodeAsync()).Should().Be(User.NotFound.Code);
    }

    /// <summary>
    /// The M6 guide expected 422 here. A body that is not JSON never becomes a
    /// command, so no validator can name a field; RFC 9110 calls that a
    /// malformed request and MVC answers 400 with problem details. 422 is kept
    /// for a well-formed body whose values are wrong — the test above.
    /// </summary>
    [Fact]
    public async Task AMalformedBody_Is400_WithProblemDetails_NotA500()
    {
        var body = new StringContent("{ this is not json", Encoding.UTF8, "application/json");

        var response = await _administrator.PostAsync("/api/v1/users", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task AnUnhandledFailure_Is500_WithATraceId_AndNoStackTrace()
    {
        var broken = Substitute.For<IPermissionLookup>();
        broken.GetPermissionCodesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("permission lookup is down"));

        await using var factory = new ApiFactory(
            fixture,
            services => services.AddScoped(_ => broken));

        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(DatabaseFixture.AdministratorEmail, DatabaseFixture.AdministratorPassword));

        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var problem = JsonDocument.Parse(payload);
        problem.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        payload.Should().NotContain("permission lookup is down").And.NotContain("InvalidOperationException");
    }
}