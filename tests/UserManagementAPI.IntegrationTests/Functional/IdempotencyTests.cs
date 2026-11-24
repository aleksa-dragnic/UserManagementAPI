using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Api.Contracts.V1;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// Idempotency-Key on POST /api/v1/users over real HTTP (ADR 0023): a retry
/// with the same key and body replays the first 201 and registers nobody new,
/// the same key with another body is a 422, a malformed key a 400, a failed
/// attempt gives the key back, and two concurrent requests with one key
/// register one user.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class IdempotencyTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string Path = "/api/v1/users";

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
    public async Task ARetry_WithTheSameKeyAndBody_ReplaysTheFirst201_AndRegistersOnce()
    {
        var body = Request("retried@example.com");

        var first = await PostAsync(body, key: "register-retried-1");
        var retry = await PostAsync(body, key: "register-retried-1");

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        retry.StatusCode.Should().Be(HttpStatusCode.Created);
        retry.Headers.Location.Should().Be(first.Headers.Location);
        (await retry.Content.ReadAsStringAsync()).Should().Be(await first.Content.ReadAsStringAsync());
        retry.Headers.GetValues("Idempotency-Replayed").Should().Equal("true");
        first.Headers.Contains("Idempotency-Replayed").Should().BeFalse();

        (await UsersWithEmailAsync("retried@example.com")).Should().Be(1);
    }

    [Fact]
    public async Task TheSameKey_WithADifferentBody_IsRefusedWith422()
    {
        (await PostAsync(Request("first@example.com"), key: "reused-key")).StatusCode
            .Should().Be(HttpStatusCode.Created);

        var reused = await PostAsync(Request("second@example.com"), key: "reused-key");

        reused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await reused.ErrorCodeAsync()).Should().Be("Idempotency.KeyReused");
        (await UsersWithEmailAsync("second@example.com")).Should().Be(0);
    }

    [Fact]
    public async Task WithoutAKey_ARepeatedRequest_BehavesAsBefore()
    {
        var body = Request("unkeyed@example.com");

        (await PostAsync(body, key: null)).StatusCode.Should().Be(HttpStatusCode.Created);
        var repeated = await PostAsync(body, key: null);

        repeated.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await repeated.ErrorCodeAsync()).Should().Be("User.EmailNotUnique");
    }

    // No empty key here: HttpClient does not send a header whose value is
    // empty, so the request would arrive without one and rightly be a 201.
    [Theory]
    [InlineData("has space")]
    [InlineData("tab\tinside")]
    public async Task AMalformedKey_IsRefusedWith400_BeforeAnythingRuns(string key)
    {
        var response = await PostAsync(Request("malformed@example.com"), key);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ErrorCodeAsync()).Should().Be("Request.InvalidIdempotencyKey");
        (await UsersWithEmailAsync("malformed@example.com")).Should().Be(0);
    }

    [Fact]
    public async Task AKeyLongerThan255Characters_IsRefusedWith400()
    {
        var response = await PostAsync(Request("long@example.com"), new string('k', 256));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ErrorCodeAsync()).Should().Be("Request.InvalidIdempotencyKey");
    }

    [Fact]
    public async Task AFailedAttempt_GivesTheKeyBack_SoACorrectedRetryRuns()
    {
        // Rejected by validation, so nothing is remembered under the key.
        var failed = await PostAsync(Request("not-an-email"), key: "corrected");
        failed.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var corrected = await PostAsync(Request("corrected@example.com"), key: "corrected");

        corrected.StatusCode.Should().Be(HttpStatusCode.Created);
        corrected.Headers.Contains("Idempotency-Replayed").Should().BeFalse();
    }

    [Fact]
    public async Task TwoConcurrentRequests_WithOneKey_RegisterOneUser()
    {
        var body = Request("concurrent@example.com");

        var responses = await Task.WhenAll(
            PostAsync(body, key: "concurrent-key"),
            PostAsync(body, key: "concurrent-key"));

        // Any correct outcome: the second either arrived while the first ran
        // (409) or after it finished (the replayed 201). Never two users.
        responses.Select(response => response.StatusCode)
            .Should().Contain(HttpStatusCode.Created)
            .And.OnlyContain(status => status == HttpStatusCode.Created || status == HttpStatusCode.Conflict);
        (await UsersWithEmailAsync("concurrent@example.com")).Should().Be(1);
    }

    private static RegisterUserRequest Request(string email) =>
        new(email, "Ana", "Petrović", "Idempotent-Passw0rd!");

    private async Task<HttpResponseMessage> PostAsync(RegisterUserRequest body, string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = JsonContent.Create(body) };

        if (key is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        }

        return await _administrator.SendAsync(request);
    }

    private async Task<int> UsersWithEmailAsync(string email)
    {
        await using var context = fixture.CreateContext();

        return await context.Users.CountAsync(user => user.Email.Value == email);
    }
}
