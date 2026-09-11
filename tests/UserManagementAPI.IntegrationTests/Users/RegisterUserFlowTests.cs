using UserManagementAPI.Application.Users.Commands.RegisterUser;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.IntegrationTests.Identity;
using UserManagementAPI.Infrastructure.Persistence.Repositories;

namespace UserManagementAPI.IntegrationTests.Users;

/// <summary>
/// The handler against the real repository, unit of work and hasher. The unit
/// tests prove the handler orchestrates; this proves what it orchestrates
/// actually lands in the database and comes back intact.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RegisterUserFlowTests(DatabaseFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegistersAUser_ThatReloadsFromAFreshContext()
    {
        const string password = "correct horse battery staple";
        var hasher = Argon2PasswordHasherTests.FastHasher();
        Guid userId;

        await using (var context = fixture.CreateContext())
        {
            var handler = new RegisterUserCommandHandler(new UserRepository(context), hasher, context);

            var result = await handler.HandleAsync(
                new RegisterUserCommand("Ana.Petrovic@Example.com", "Ana", "Petrović", password));

            result.IsSuccess.Should().BeTrue();
            userId = result.Value;
        }

        await using var readContext = fixture.CreateContext();
        var reloaded = await new UserRepository(readContext).GetByIdAsync(userId);

        reloaded.Should().NotBeNull();
        reloaded!.Email.Value.Should().Be("ana.petrovic@example.com");
        reloaded.Name.FullName.Should().Be("Ana Petrović");
        reloaded.Status.Should().Be(UserStatus.Pending);
        reloaded.PasswordHash.Value.Should().NotContain(password);
        hasher.Verify(password, reloaded.PasswordHash.Value).Should().BeTrue();
        hasher.Verify("wrong password", reloaded.PasswordHash.Value).Should().BeFalse();
    }
}