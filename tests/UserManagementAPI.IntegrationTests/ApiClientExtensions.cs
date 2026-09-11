using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Api.Contracts.V1;

namespace UserManagementAPI.IntegrationTests;

/// <summary>
/// The steps functional tests repeat: register a user and grant a role through
/// the API as the administrator, look up a seeded role, read the error code out
/// of a problem details body.
/// </summary>
internal static class ApiClientExtensions
{
    public static async Task<Guid> RegisterUserAsync(
        this HttpClient administrator,
        string email,
        string password,
        string firstName = "Test",
        string lastName = "User")
    {
        var response = await administrator.PostAsJsonAsync(
            "/api/v1/users",
            new RegisterUserRequest(email, firstName, lastName, password));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<UserCreatedResponse>();

        return created!.Id;
    }

    public static async Task AssignRoleAsync(this HttpClient administrator, Guid userId, Guid roleId)
    {
        var response = await administrator.PostAsJsonAsync(
            $"/api/v1/users/{userId}/roles",
            new AssignRoleRequest(roleId));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    public static async Task<Guid> RoleIdAsync(this DatabaseFixture fixture, string roleName)
    {
        await using var context = fixture.CreateContext();

        return await context.Roles
            .Where(role => role.Name == roleName)
            .Select(role => role.Id)
            .SingleAsync();
    }

    /// <summary>The stable "errorCode" extension every problem details body carries.</summary>
    public static async Task<string?> ErrorCodeAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.TryGetProperty("errorCode", out var errorCode)
            ? errorCode.GetString()
            : null;
    }
}