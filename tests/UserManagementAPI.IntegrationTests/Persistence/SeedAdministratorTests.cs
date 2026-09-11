using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using UserManagementAPI.Domain.Users;
using UserManagementAPI.Infrastructure.Persistence.Seed;

namespace UserManagementAPI.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class SeedAdministratorTests(DatabaseFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SeedsAnActiveAdministrator_HoldingTheAdministratorRole_Once()
    {
        await using var provider = fixture.CreateServiceProvider();

        await SeedAsync(provider);
        await SeedAsync(provider);

        await using var context = fixture.CreateContext();

        var administrator = await context.Users.Include(user => user.Roles).SingleAsync();
        var administratorRole = await context.Roles.SingleAsync(role => role.Name == RoleNames.Administrator);

        administrator.Email.Value.Should().Be(DatabaseFixture.AdministratorEmail);
        administrator.Status.Should().Be(UserStatus.Active);
        administrator.Roles.Should().ContainSingle().Which.RoleId.Should().Be(administratorRole.Id);
        administrator.PasswordHash.Value.Should().StartWith("$argon2id$");
        administrator.PasswordHash.Value.Should().NotContain(DatabaseFixture.AdministratorPassword);
    }

    [Fact]
    public async Task SeedsNoAdministrator_WhenNoPasswordIsConfigured()
    {
        await using var provider = fixture.CreateServiceProvider(
            settings: new Dictionary<string, string?> { ["Seed:AdministratorPassword"] = "" });

        await SeedAsync(provider);

        await using var context = fixture.CreateContext();
        context.Users.Should().BeEmpty();
        context.Roles.Should().HaveCount(2);
    }

    private static async Task SeedAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var seeder = ActivatorUtilities.CreateInstance<DatabaseSeeder>(scope.ServiceProvider);
        await seeder.SeedAsync();
    }
}