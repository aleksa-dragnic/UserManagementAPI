using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

using UserManagementAPI.Api.Contracts.V1;

namespace UserManagementAPI.IntegrationTests;

/// <summary>
/// The steps functional tests repeat: register a user and grant a role through
/// the API as the administrator, look up a seeded role, read the error code out
/// of a problem details body, and carry the refresh cookie by hand.
/// </summary>
internal static class ApiClientExtensions
{
    /// <summary>The refresh cookie's name, as the contract fixes it (ADR 0019).</summary>
    public const string RefreshCookieName = "umapi_rt";

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

    /// <summary>The umapi_rt cookie a response sets or clears, or null when it does neither.</summary>
    public static SetCookieHeaderValue? RefreshCookie(this HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? SetCookieHeaderValue.ParseList(values.ToList()).SingleOrDefault(cookie => cookie.Name.Value == RefreshCookieName)
            : null;

    /// <summary>The refresh token a successful login or refresh set as its cookie.</summary>
    public static string RefreshTokenFromCookie(this HttpResponseMessage response)
    {
        var cookie = response.RefreshCookie();

        cookie.Should().NotBeNull("a successful login or refresh sets the refresh cookie");
        cookie!.Value.Value.Should().NotBeNullOrEmpty();

        return cookie.Value.Value!;
    }

    /// <summary>A POST with no body, presenting the given refresh token as the umapi_rt cookie, or no cookie at all.</summary>
    public static Task<HttpResponseMessage> PostWithRefreshCookieAsync(
        this HttpClient client,
        string path,
        string? refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);

        if (refreshToken is not null)
        {
            request.Headers.Add("Cookie", $"{RefreshCookieName}={refreshToken}");
        }

        return client.SendAsync(request);
    }
}