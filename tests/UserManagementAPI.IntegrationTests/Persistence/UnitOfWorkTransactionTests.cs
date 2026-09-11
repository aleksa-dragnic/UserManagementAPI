using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.IntegrationTests.Persistence;

/// <summary>
/// The transaction behavior delegates to AppDbContext.ExecuteInTransactionAsync;
/// these tests prove the commit and rollback semantics against a real database,
/// which a unit test over a fake unit of work cannot.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class UnitOfWorkTransactionTests(DatabaseFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CommitsTheWrite_WhenTheOperationSucceeds()
    {
        await using (var context = fixture.CreateContext())
        {
            await context.ExecuteInTransactionAsync(async () =>
            {
                context.Users.Add(NewUser("committed@example.com"));
                await context.SaveChangesAsync();
                return Result.Success();
            });
        }

        await using var verifyContext = fixture.CreateContext();
        verifyContext.Users.Should().ContainSingle();
    }

    [Fact]
    public async Task RollsBackTheWrite_WhenTheOperationReturnsAFailedResult()
    {
        await using (var context = fixture.CreateContext())
        {
            var response = await context.ExecuteInTransactionAsync(async () =>
            {
                context.Users.Add(NewUser("rolled.back@example.com"));
                await context.SaveChangesAsync();
                return Result.Failure(new Error("Test.Rollback", "Deliberate failure after the write."));
            });

            response.IsFailure.Should().BeTrue();
        }

        await using var verifyContext = fixture.CreateContext();
        verifyContext.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task RollsBackTheWrite_WhenTheOperationThrows()
    {
        await using (var context = fixture.CreateContext())
        {
            var act = async () => await context.ExecuteInTransactionAsync<Result>(async () =>
            {
                context.Users.Add(NewUser("thrown@example.com"));
                await context.SaveChangesAsync();
                throw new InvalidOperationException("Deliberate failure after the write.");
            });

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        await using var verifyContext = fixture.CreateContext();
        verifyContext.Users.Should().BeEmpty();
    }

    private static User NewUser(string email) => User.Register(
        Email.Create(email).Value,
        PersonName.Create("Ana", "Petrović").Value,
        PasswordHash.Create("argon2id$hash").Value).Value;
}