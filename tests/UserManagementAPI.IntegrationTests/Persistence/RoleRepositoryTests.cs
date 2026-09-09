using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Infrastructure.Persistence.Repositories;

namespace UserManagementAPI.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class RoleRepositoryTests(DatabaseFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ARoleRoundTripsWithItsPermissions()
    {
        var permission = Permission.Create("users.read").Value;
        var role = Role.Create("Administrator").Value;

        await using (var writeContext = fixture.CreateContext())
        {
            writeContext.Permissions.Add(permission);
            role.AddPermission(permission.Id);
            writeContext.Roles.Add(role);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var reloaded = await new RoleRepository(readContext).GetByNameAsync("Administrator");

        reloaded.Should().NotBeNull();
        reloaded!.Permissions.Should().ContainSingle()
            .Which.PermissionId.Should().Be(permission.Id);
    }
}