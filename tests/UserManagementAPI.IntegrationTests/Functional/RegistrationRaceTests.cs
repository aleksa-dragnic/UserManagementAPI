using System.Net;
using System.Net.Http.Json;

using UserManagementAPI.Api.Contracts.V1;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// Registration checks for a taken email and then inserts. Requests sent
/// together can all pass the check; the unique index stops all but one, and
/// the others must still be told 409 with the domain's error code, not 500.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RegistrationRaceTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private ApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture);
        await _factory.ResetAndSeedAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task ConcurrentRegistrations_OfOneEmail_CreateOneUser_AndRefuseTheRestWith409()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();
        var request = new RegisterUserRequest("raced@example.com", "Ana", "Petrović", "Raced-Passw0rd-long");

        var responses = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => administrator.PostAsJsonAsync("/api/v1/users", request)));

        responses.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.Created);
        var refused = responses.Where(response => response.StatusCode != HttpStatusCode.Created).ToList();
        refused.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Conflict);

        foreach (var response in refused)
        {
            (await response.ErrorCodeAsync()).Should().Be("User.EmailNotUnique");
        }
    }
}
