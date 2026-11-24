using System.Net;
using System.Net.Http.Json;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Domain.Roles;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// A user's history over real HTTP (ADR 0025): every change the API makes to a
/// user, newest first, by field name and never by value, with the actor's email
/// and the role's name; readable with users.read alone; 404 for an unknown user.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class UserHistoryTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string Password = "History-Passw0rd!";

    private ApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture);
        await _factory.ResetAndSeedAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task EveryChange_IsReported_NewestFirst_WithItsActorAndRole()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();
        var userId = await administrator.RegisterUserAsync("history.before@example.com", Password, "Ana", "Petrovic");
        var memberId = await fixture.RoleIdAsync(RoleNames.Member);
        var administratorRoleId = await fixture.RoleIdAsync(RoleNames.Administrator);

        (await administrator.PutAsJsonAsync(
                $"/api/v1/users/{userId}",
                new UpdateUserRequest("history.after@example.com", "Ana", "Jovanovic")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await administrator.PostAsync($"/api/v1/users/{userId}/lock", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        await administrator.AssignRoleAsync(userId, administratorRoleId);
        (await administrator.DeleteAsync($"/api/v1/users/{userId}/roles/{memberId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var history = await HistoryAsync(administrator, userId);

        history.Select(entry => entry.Action).Should().Equal(
            "roleRemoved", "roleAssigned", "updated", "updated", "roleAssigned", "registered");
        history[0].RoleName.Should().Be(RoleNames.Member);
        history[1].RoleName.Should().Be(RoleNames.Administrator);
        history[2].Fields.Should().Equal("status");
        history[3].Fields.Should().BeEquivalentTo("email", "lastName");
        history[4].RoleName.Should().Be(RoleNames.Member, "registration assigns Member");
        history.Should().OnlyContain(entry => entry.ActorEmail == DatabaseFixture.AdministratorEmail);
        history.Should().BeInDescendingOrder(entry => entry.OccurredAtUtc);
    }

    [Fact]
    public async Task TheHistory_NamesFields_AndCarriesNoValue()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();
        var userId = await administrator.RegisterUserAsync("values.before@example.com", Password, "Marko", "Ilic");
        await administrator.PutAsJsonAsync(
            $"/api/v1/users/{userId}",
            new UpdateUserRequest("values.after@example.com", "Marko", "Ilic"));

        var body = await administrator.GetStringAsync($"/api/v1/users/{userId}/history");

        body.Should().Contain("\"email\"")
            .And.NotContain("values.before@example.com")
            .And.NotContain("values.after@example.com")
            .And.NotContain(Password)
            .And.NotContain("Marko");
    }

    [Fact]
    public async Task AnAccountWithUsersReadAlone_ReadsIt()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();
        var userId = await administrator.RegisterUserAsync("reader.history@example.com", Password);
        var member = await _factory.CreateClientAsAsync("reader.history@example.com", Password);

        var response = await member.GetAsync($"/api/v1/users/{userId}/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ASeededUser_ReadsAsRegisteredBySystem()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();
        var me = await administrator.GetFromJsonAsync<UserResponse[]>(
            $"/api/v1/users?searchTerm={Uri.EscapeDataString(DatabaseFixture.AdministratorEmail)}");

        var history = await HistoryAsync(administrator, me!.Single().Id);

        history.Should().Contain(entry => entry.Action == "registered" && entry.ActorId == null && entry.ActorEmail == null);
    }

    [Fact]
    public async Task AnUnknownUser_IsA404()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();

        var response = await administrator.GetAsync($"/api/v1/users/{Guid.NewGuid()}/history");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.ErrorCodeAsync()).Should().Be("User.NotFound");
    }

    [Fact]
    public async Task WithoutAToken_ItIsA401()
    {
        var anonymous = _factory.CreateClientWithExplicitCookies();

        var response = await anonymous.GetAsync($"/api/v1/users/{Guid.NewGuid()}/history");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReadAgainUnchanged_ItIsA304()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();
        var userId = await administrator.RegisterUserAsync("etag.history@example.com", Password);
        var first = await administrator.GetAsync($"/api/v1/users/{userId}/history");

        using var again = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/users/{userId}/history");
        again.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var response = await administrator.SendAsync(again);

        response.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    private static async Task<UserHistoryEntryResponse[]> HistoryAsync(HttpClient client, Guid userId) =>
        (await client.GetFromJsonAsync<UserHistoryEntryResponse[]>($"/api/v1/users/{userId}/history"))!;
}
