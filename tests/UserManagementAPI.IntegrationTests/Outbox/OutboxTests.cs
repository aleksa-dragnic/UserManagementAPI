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
    private Guid _memberRoleId;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        _memberRoleId = await TestRoles.MemberIdAsync(fixture);
    }

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

    [Fact]
    public async Task TwoProcessorsPollingAtOnce_PublishEachMessageOnce()
    {
        // Two service providers stand in for two replicas: separate containers,
        // separate connections, one database.
        var holding = new HoldingPublisher();
        var second = new RecordingPublisher();
        await using var first = fixture.CreateServiceProvider(services =>
            services.AddSingleton<IOutboxPublisher>(holding));
        await using var other = fixture.CreateServiceProvider(services =>
            services.AddSingleton<IOutboxPublisher>(second));

        foreach (var email in new[] { "one@example.com", "two@example.com", "three@example.com" })
        {
            await RegisterAsync(first, email);
        }

        // The first processor claims the batch and stops inside its first
        // publish, holding the claim; the second polls while it is held.
        var firstBatch = ProcessBatchAsync(first);
        await holding.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        (await ProcessBatchAsync(other)).Should().Be(0);
        second.Published.Should().BeEmpty();

        holding.Release.SetResult();
        (await firstBatch).Should().Be(3);

        holding.Published.Select(message => message.Id).Should().OnlyHaveUniqueItems().And.HaveCount(3);

        await using var verifyContext = fixture.CreateContext();
        verifyContext.OutboxMessages.Should().HaveCount(3)
            .And.OnlyContain(message => message.ProcessedOnUtc != null);
    }

    [Fact]
    public async Task ASecondProcessor_TakesTheRowsTheFirstDidNotClaim()
    {
        // A batch of two over three messages: the first processor holds the two
        // oldest, and the second is not blocked by them - it takes the third.
        var batchOfTwo = new Dictionary<string, string?> { ["Outbox:BatchSize"] = "2" };
        var holding = new HoldingPublisher();
        var second = new RecordingPublisher();
        await using var first = fixture.CreateServiceProvider(
            services => services.AddSingleton<IOutboxPublisher>(holding), batchOfTwo);
        await using var other = fixture.CreateServiceProvider(
            services => services.AddSingleton<IOutboxPublisher>(second), batchOfTwo);

        foreach (var email in new[] { "one@example.com", "two@example.com", "three@example.com" })
        {
            await RegisterAsync(first, email);
        }

        var firstBatch = ProcessBatchAsync(first);
        await holding.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        (await ProcessBatchAsync(other)).Should().Be(1);

        holding.Release.SetResult();
        (await firstBatch).Should().Be(2);

        holding.Published.Select(message => message.Id)
            .Should().NotIntersectWith(second.Published.Select(message => message.Id));
    }

    private async Task RegisterAsync(ServiceProvider provider, string email)
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

    private User NewUser(string email) => User.Register(
        Email.Create(email).Value,
        PersonName.Create("Ana", "Petrović").Value,
        PasswordHash.Create("pbkdf2-sha256$1$c2FsdA==$aGFzaA==").Value,
        _memberRoleId).Value;

    private sealed class RecordingPublisher : IOutboxPublisher
    {
        public List<OutboxMessage> Published { get; } = [];

        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            Published.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Records what it publishes, and blocks inside the first publish until the
    /// test releases it, so a batch can be held open at a known point.
    /// </summary>
    private sealed class HoldingPublisher : IOutboxPublisher
    {
        public List<OutboxMessage> Published { get; } = [];

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            Published.Add(message);
        }
    }

    private sealed class ThrowingPublisher : IOutboxPublisher
    {
        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Broker unavailable.");
    }
}