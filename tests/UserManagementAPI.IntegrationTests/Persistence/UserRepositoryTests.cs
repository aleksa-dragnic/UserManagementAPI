using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.Infrastructure.Persistence.Repositories;

namespace UserManagementAPI.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class UserRepositoryTests(DatabaseFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AUserRoundTrips_WithEveryValueObjectIntact()
    {
        var user = NewUser("ana.petrovic@example.com");
        user.VerifyEmail();

        await using (var writeContext = fixture.CreateContext())
        {
            new UserRepository(writeContext).Add(user);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var reloaded = await new UserRepository(readContext).GetByIdAsync(user.Id);

        reloaded.Should().NotBeNull();
        reloaded!.Email.Should().Be(user.Email);
        reloaded.Name.Should().Be(user.Name);
        reloaded.PasswordHash.Should().Be(user.PasswordHash);
        reloaded.Status.Should().Be(UserStatus.Active);
        reloaded.CreatedAtUtc.Should().BeCloseTo(user.CreatedAtUtc, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Status_ConvertsToAndFromItsIntId()
    {
        var user = NewUser("locked.user@example.com");
        user.VerifyEmail();
        user.Lock();

        await using (var writeContext = fixture.CreateContext())
        {
            writeContext.Users.Add(user);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();

        var storedId = await readContext.Database
            .SqlQuery<int>($"select status_id as \"Value\" from users where id = {user.Id}")
            .SingleAsync();

        storedId.Should().Be(UserStatus.Locked.Id);

        var reloaded = await readContext.Users.SingleAsync(candidate => candidate.Id == user.Id);
        reloaded.Status.Should().Be(UserStatus.Locked);
    }

    [Fact]
    public async Task ADuplicateEmail_ViolatesTheUniqueIndex()
    {
        await using (var seedContext = fixture.CreateContext())
        {
            seedContext.Users.Add(NewUser("taken@example.com"));
            await seedContext.SaveChangesAsync();
        }

        await using var context = fixture.CreateContext();
        // Different casing: citext must treat this as the same address.
        context.Users.Add(NewUser("TAKEN@example.com"));

        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ExistsByEmail_IgnoresCase()
    {
        await using (var seedContext = fixture.CreateContext())
        {
            seedContext.Users.Add(NewUser("ana.petrovic@example.com"));
            await seedContext.SaveChangesAsync();
        }

        await using var context = fixture.CreateContext();
        var repository = new UserRepository(context);

        (await repository.ExistsByEmailAsync(Email.Create("ANA.PETROVIC@EXAMPLE.COM").Value))
            .Should().BeTrue();
        (await repository.ExistsByEmailAsync(Email.Create("someone.else@example.com").Value))
            .Should().BeFalse();
    }

    [Fact]
    public async Task RoleAssignments_RoundTripWithTheAggregate()
    {
        // The role must exist: user_roles.role_id is a foreign key to roles.
        var role = Role.Create("Member").Value;
        var user = NewUser("with.roles@example.com");
        user.VerifyEmail();
        user.AssignRole(role.Id);

        await using (var writeContext = fixture.CreateContext())
        {
            writeContext.Roles.Add(role);
            writeContext.Users.Add(user);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var reloaded = await new UserRepository(readContext).GetByIdAsync(user.Id);

        reloaded!.Roles.Should().ContainSingle().Which.RoleId.Should().Be(role.Id);
    }

    private static User NewUser(string email) => User.Register(
        Email.Create(email).Value,
        PersonName.Create("Ana", "Petrović").Value,
        PasswordHash.Create("argon2id$hash").Value).Value;
}