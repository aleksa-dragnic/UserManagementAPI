using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.Domain.Users.Events;

namespace UserManagementAPI.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class DomainEventDispatchTests(DatabaseFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EventsAreDispatchedOnce_AndClearedFromTheAggregate()
    {
        var publisher = new RecordingPublisher();
        var user = NewUser("dispatch@example.com");

        await using var context = fixture.CreateContext(publisher);
        context.Users.Add(user);

        await context.SaveChangesAsync();
        // A second save must not replay anything.
        await context.SaveChangesAsync();

        publisher.Published.Should().ContainSingle()
            .Which.Should().BeOfType<UserRegisteredDomainEvent>()
            .Which.UserId.Should().Be(user.Id);

        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task AThrowingHandlerRollsBackTheWrite()
    {
        var user = NewUser("rollback@example.com");

        await using (var context = fixture.CreateContext(new ThrowingPublisher()))
        {
            context.Users.Add(user);

            var act = async () => await context.SaveChangesAsync();

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        await using var verifyContext = fixture.CreateContext();
        verifyContext.Users.Should().BeEmpty();
    }

    private static User NewUser(string email) => User.Register(
        Email.Create(email).Value,
        PersonName.Create("Ana", "Petrović").Value,
        PasswordHash.Create("argon2id$hash").Value).Value;

    private sealed class RecordingPublisher : IDomainEventPublisher
    {
        public List<IDomainEvent> Published { get; } = [];

        public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            Published.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPublisher : IDomainEventPublisher
    {
        public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Handler failed.");
    }
}