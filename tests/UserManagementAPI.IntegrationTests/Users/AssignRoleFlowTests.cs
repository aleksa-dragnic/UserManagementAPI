using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.Infrastructure.Persistence.Repositories;

namespace UserManagementAPI.IntegrationTests.Users;

/// <summary>
/// A role assigned to a user that was loaded from the database, not built in
/// memory. EF Core discovers the new UserRole through the Roles navigation; with
/// a store-generated key it would track the child as Modified and fail the
/// UPDATE with a concurrency exception. Ids are domain-generated, so the child
/// must be inserted.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AssignRoleFlowTests(DatabaseFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AssignRole_OnAReloadedUser_InsertsTheAssignment()
    {
        var role = Role.Create("Member").Value;
        var user = User.Register(
            Email.Create("reloaded@example.com").Value,
            PersonName.Create("Ana", "Petrović").Value,
            PasswordHash.Create("argon2id$hash").Value).Value;
        user.VerifyEmail();

        await using (var seedContext = fixture.CreateContext())
        {
            seedContext.Roles.Add(role);
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();
        }

        await using (var writeContext = fixture.CreateContext())
        {
            var repository = new UserRepository(writeContext);
            var loaded = await repository.GetByIdAsync(user.Id);

            loaded!.AssignRole(role.Id).IsSuccess.Should().BeTrue();
            repository.Update(loaded);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var reloaded = await new UserRepository(readContext).GetByIdAsync(user.Id);

        reloaded!.Roles.Should().ContainSingle().Which.RoleId.Should().Be(role.Id);
    }
}