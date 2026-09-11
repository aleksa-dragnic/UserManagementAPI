using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// The read side over real HTTP: GET users and roles, gated on users.read and
/// roles.read, projected from the database.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ReadEndpointTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string ReaderEmail = "reader@example.com";

    private const string ReaderPassword = "Reader-Passw0rd!";

    private ApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture);
        await _factory.ResetAndSeedAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task GetUser_ReturnsTheStoredRow_WithTheNamesOfItsRoles()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();
        var readerId = await administrator.RegisterUserAsync(ReaderEmail, ReaderPassword, "Ana", "Petrović");
        await administrator.AssignRoleAsync(readerId, await fixture.RoleIdAsync(RoleNames.Member));

        var response = await administrator.GetAsync($"/api/v1/users/{readerId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserDetailsResponse>();

        await using var context = fixture.CreateContext();
        var stored = await context.Users.SingleAsync(candidate => candidate.Id == readerId);

        user!.Id.Should().Be(stored.Id);
        user.Email.Should().Be(stored.Email.Value);
        user.FirstName.Should().Be("Ana");
        user.LastName.Should().Be("Petrović");
        user.Status.Should().Be(UserStatus.Pending.Name);
        user.Roles.Should().ContainSingle().Which.Name.Should().Be(RoleNames.Member);
        user.CreatedAtUtc.Should().BeCloseTo(stored.CreatedAtUtc, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task GetUser_ReturnsA404ProblemDetails_ForAnUnknownId()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();

        var response = await administrator.GetAsync($"/api/v1/users/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.ErrorCodeAsync()).Should().Be(User.NotFound.Code);
    }

    [Fact]
    public async Task GetUsers_IsAllowedForAMember_AndListsEveryUserByEmail()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();
        var readerId = await administrator.RegisterUserAsync(ReaderEmail, ReaderPassword);
        await administrator.AssignRoleAsync(readerId, await fixture.RoleIdAsync(RoleNames.Member));
        var reader = await _factory.CreateClientAsAsync(ReaderEmail, ReaderPassword);

        var response = await reader.GetAsync("/api/v1/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<List<UserResponse>>();
        users!.Select(user => user.Email).Should().Equal(DatabaseFixture.AdministratorEmail, ReaderEmail);
    }

    [Fact]
    public async Task GetUsers_IsForbidden_ForAUserWithoutARole()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();
        await administrator.RegisterUserAsync(ReaderEmail, ReaderPassword);
        var roleless = await _factory.CreateClientAsAsync(ReaderEmail, ReaderPassword);

        var response = await roleless.GetAsync("/api/v1/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetRoles_ListsTheSeededRoles_WithTheirPermissionCodes()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();

        var response = await administrator.GetAsync("/api/v1/roles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        List<RoleResponse> roles = (await response.Content.ReadFromJsonAsync<List<RoleResponse>>())!;

        roles.Select(role => role.Name).Should().Equal(RoleNames.Administrator, RoleNames.Member);
        roles[0].Permissions.Should().Equal(PermissionCodes.All.Order(StringComparer.Ordinal));
        roles[1].Permissions.Should().Equal(PermissionCodes.RolesRead, PermissionCodes.UsersRead);
    }

    [Fact]
    public async Task GetRole_ReturnsA404ProblemDetails_ForAnUnknownId()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();

        var response = await administrator.GetAsync($"/api/v1/roles/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.ErrorCodeAsync()).Should().Be(Role.NotFound.Code);
    }
}