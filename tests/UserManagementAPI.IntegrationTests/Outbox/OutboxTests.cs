using Microsoft.Extensions.DependencyInjection;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.Domain.Users.Events;
using UserManagementAPI.Infrastructure.Outbox;
using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.IntegrationTests.Outbox;

/// <summary>
/// Runs through the production wiring: the interceptor dispatches
/// UserRegisteredDomainEvent, the handler writes an outbox message through the
/// same DbContext, and the batch processor publishes it later.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OutboxTests(DatabaseFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AMessageIsWritten_InTheSameSaveAsTheUser()
    {
        await using var provider = fixture.CreateServiceProvider();
        var user = NewUser("outbox@example.com");

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        await using var verifyContext = fixture.CreateContext();

        var message = verifyContext.OutboxMessages.Should().ContainSingle().Subject;
        message.Type.Should().Be(typeof(UserRegisteredDomainEvent).FullName);
        message.Payload.Should().Contain(user.Id.ToString());
        message.ProcessedOnUtc.Should().BeNull();
        message.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task AMessageIsAbsent_WhenTheTransactionRollsBack()
    {
        await using var provider = fixture.CreateServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var response = await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                context.Users.Add(NewUser("rolled.back@example.com"));
                await context.SaveChangesAsync();
                return Result.Failure(new Error("Test.Rollback", "Deliberate failure after the write."));
            });

            response.IsFailure.Should().BeTrue();
        }

        await using var verifyContext = fixture.CreateContext();
        verifyContext.Users.Should().BeEmpty();
        verifyContext.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task TheProcessorMarksAMessageProcessed_ExactlyOnce()
    {
        var publisher = new RecordingPublisher();
        await using var provider = fixture.CreateServiceProvider(services =>
            services.AddSingleton<IOutboxPublisher>(publisher));

        await RegisterAsync(provider, "processed@example.com");

        (await ProcessBatchAsync(provider)).Should().Be(1);
        (await ProcessBatchAsync(provider)).Should().Be(0);

        publisher.Published.Should().ContainSingle();

        await using var verifyContext = fixture.CreateContext();
        var message = verifyContext.OutboxMessages.Should().ContainSingle().Subject;
        message.ProcessedOnUtc.Should().NotBeNull();
        message.Error.Should().BeNull();
    }

    [Fact]
    public async Task AFailingMessageIsRetried_ThenAbandonedAtTheCeiling()
    {
        await using var provider = fixture.CreateServiceProvider(services =>
            services.AddSingleton<IOutboxPublisher>(new ThrowingPublisher()));

        await RegisterAsync(provider, "poison@example.com");

        // Three attempts (the fixture's ceiling), each backing off by zero, then
        // a fourth poll that must not pick the message up again.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            (await ProcessBatchAsync(provider)).Should().Be(0);
        }

        await using var verifyContext = fixture.CreateContext();
        var message = verifyContext.OutboxMessages.Should().ContainSingle().Subject;
        message.ProcessedOnUtc.Should().BeNull();
        message.Attempts.Should().Be(3);
        message.Error.Should().Contain("Broker unavailable");
        message.NextAttemptOnUtc.Should().NotBeNull();
    }

    private static async Task RegisterAsync(ServiceProvider provider, string email)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Users.Add(NewUser(email));
        await context.SaveChangesAsync();
    }

    private static async Task<int> ProcessBatchAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OutboxBatchProcessor>().ProcessBatchAsync();
    }

    private static User NewUser(string email) => User.Register(
        Email.Create(email).Value,
        PersonName.Create("Ana", "Petrović").Value,
        PasswordHash.Create("pbkdf2-sha256$1$c2FsdA==$aGFzaA==").Value).Value;

    private sealed class RecordingPublisher : IOutboxPublisher
    {
        public List<OutboxMessage> Published { get; } = [];

        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            Published.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPublisher : IOutboxPublisher
    {
        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Broker unavailable.");
    }
}