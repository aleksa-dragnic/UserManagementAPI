using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Domain.Roles;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// Real HTTP through the whole stack. The administrator registers a user and
/// gives them Member; the Member then tries to write.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PermissionGateTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string MemberEmail = "member@example.com";

    private const string MemberPassword = "Member-Passw0rd!";

    private ApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture);
        await _factory.ResetAndSeedAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task AnUnauthenticatedRequest_GetsA401_AsProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/users",
            new RegisterUserRequest("x@example.com", "X", "Y", "Passw0rd-long-enough"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task AMember_GetsA403_OnAWriteEndpoint()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();
        var memberId = await RegisterMemberAsync(administrator);

        var member = await _factory.CreateClientAsAsync(MemberEmail, MemberPassword);

        var update = await member.PutAsJsonAsync(
            $"/api/v1/users/{memberId}",
            new UpdateUserRequest(MemberEmail, "Renamed", "Member"));
        var register = await member.PostAsJsonAsync(
            "/api/v1/users",
            new RegisterUserRequest("another@example.com", "An", "Other", "Passw0rd-long-enough"));

        update.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        register.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await update.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task TheAdministrator_WithThePermission_Succeeds()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();
        var memberId = await RegisterMemberAsync(administrator);

        var lock_ = await administrator.PostAsync($"/api/v1/users/{memberId}/lock", content: null);
        var unlock = await administrator.DeleteAsync($"/api/v1/users/{memberId}/lock");

        lock_.StatusCode.Should().Be(HttpStatusCode.NoContent);
        unlock.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Registers a user and grants Member through the API, as the administrator.</summary>
    private async Task<Guid> RegisterMemberAsync(HttpClient administrator)
    {
        var registered = await administrator.PostAsJsonAsync(
            "/api/v1/users",
            new RegisterUserRequest(MemberEmail, "Mem", "Ber", MemberPassword));
        registered.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await registered.Content.ReadFromJsonAsync<UserCreatedResponse>();

        Guid memberRoleId;
        await using (var context = fixture.CreateContext())
        {
            memberRoleId = await context.Roles
                .Where(role => role.Name == RoleNames.Member)
                .Select(role => role.Id)
                .SingleAsync();
        }

        var assigned = await administrator.PostAsJsonAsync(
            $"/api/v1/users/{created!.Id}/roles",
            new AssignRoleRequest(memberRoleId));
        assigned.StatusCode.Should().Be(HttpStatusCode.NoContent);

        return created.Id;
    }
}