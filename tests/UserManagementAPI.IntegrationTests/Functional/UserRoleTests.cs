using System.Net;
using System.Net.Http.Json;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Domain.Roles;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// A user's roles over real HTTP: registration gives the Member role, a role
/// can be removed while another remains, and the last one cannot be removed.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class UserRoleTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private ApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture);
        await _factory.ResetAndSeedAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task ARegisteredUser_StartsWithTheMemberRole()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();
        var userId = await administrator.RegisterUserAsync("new.member@example.com", "New-Member-Passw0rd!");

        var user = await administrator.GetFromJsonAsync<UserDetailsResponse>($"/api/v1/users/{userId}");

        user!.Roles.Should().ContainSingle().Which.Name.Should().Be(RoleNames.Member);
    }

    [Fact]
    public async Task RemovingARole_Succeeds_WhileAnotherRemains_AndIsRefusedForTheLastOne()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();
        var userId = await administrator.RegisterUserAsync("two.roles@example.com", "Two-Roles-Passw0rd!");
        var memberId = await fixture.RoleIdAsync(RoleNames.Member);
        var administratorId = await fixture.RoleIdAsync(RoleNames.Administrator);
        await administrator.AssignRoleAsync(userId, administratorId);

        var first = await administrator.DeleteAsync($"/api/v1/users/{userId}/roles/{memberId}");
        var last = await administrator.DeleteAsync($"/api/v1/users/{userId}/roles/{administratorId}");

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        last.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await last.ErrorCodeAsync()).Should().Be("User.LastRoleCannotBeRemoved");

        var user = await administrator.GetFromJsonAsync<UserDetailsResponse>($"/api/v1/users/{userId}");
        user!.Roles.Should().ContainSingle().Which.RoleId.Should().Be(administratorId);
    }
}
