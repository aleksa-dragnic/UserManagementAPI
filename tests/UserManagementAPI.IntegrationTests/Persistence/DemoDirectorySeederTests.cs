using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.Infrastructure.Persistence.Seed;

namespace UserManagementAPI.IntegrationTests.Persistence;

/// <summary>
/// The synthetic directory the deployed demo and local runs browse: seeded
/// through the aggregate, once, in every status, and impossible to sign in as.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class DemoDirectorySeederTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string SeededDomain = "@" + DemoDirectorySeeder.EmailDomain;

    private ApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture);
        await _factory.ResetAndSeedAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task SeedsTheDirectoryOnce_AndASecondRunAddsNothing()
    {
        var first = await SeedAsync();
        var second = await SeedAsync();

        await using var context = fixture.CreateContext();
        var seeded = await context.Users.CountAsync(user => user.Email.Value.EndsWith(SeededDomain));

        first.Should().Be(DemoDirectorySeeder.UserCount);
        second.Should().Be(0);
        seeded.Should().Be(DemoDirectorySeeder.UserCount);
    }

    [Fact]
    public async Task CoversEveryStatus_WithMoreThanOnePageOfEach_AndKeepsTheDiacritics()
    {
        await SeedAsync();

        await using var context = fixture.CreateContext();
        var users = await context.Users
            .Include(user => user.Roles)
            .Where(user => user.Email.Value.EndsWith(SeededDomain))
            .ToListAsync();

        foreach (var status in new[] { UserStatus.Pending, UserStatus.Active, UserStatus.Locked, UserStatus.Deactivated })
        {
            users.Count(user => user.Status == status)
                .Should().BeGreaterThan(10, $"{status.Name} needs more than one page of ten");
        }

        users.Should().Contain(user => user.Name.Last == "Đorđević");
        users.Should().OnlyContain(user => user.Roles.Count >= 1);
        users.Should().Contain(user => user.Roles.Count == 2);
    }

    [Fact]
    public async Task ASeededUser_CannotSignIn()
    {
        await SeedAsync();

        var response = await _factory.CreateClientWithExplicitCookies().PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("ana.petrovic" + SeededDomain, DatabaseFixture.AdministratorPassword));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.ErrorCodeAsync()).Should().Be("Auth.InvalidCredentials");
    }

    private async Task<int> SeedAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        return await ActivatorUtilities.CreateInstance<DemoDirectorySeeder>(scope.ServiceProvider).SeedAsync();
    }
}