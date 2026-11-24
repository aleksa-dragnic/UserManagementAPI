using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Infrastructure.Idempotency;

namespace UserManagementAPI.IntegrationTests.Persistence;

/// <summary>
/// The store against PostgreSQL, with the clock in the test's hands: keys are
/// scoped per caller, an expired key is claimed again, and a claim whose
/// request died is taken over once its lease runs out and not before (ADR 0023).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class IdempotencyStoreTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly IdempotencyOptions Options = new()
    {
        Retention = TimeSpan.FromHours(24),
        Lease = TimeSpan.FromMinutes(1)
    };

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TheSameKey_FromTwoCallers_IsTwoKeys()
    {
        var store = Store();

        (await store.ClaimAsync(Scope(Guid.NewGuid()), "hash")).Should().BeOfType<IdempotencyClaim.Claimed>();
        (await store.ClaimAsync(Scope(Guid.NewGuid()), "hash")).Should().BeOfType<IdempotencyClaim.Claimed>();
    }

    [Fact]
    public async Task ACompletedKey_ReplaysForTheSameHash_AndIsAMismatchForAnother()
    {
        var store = Store();
        var scope = Scope(Guid.NewGuid());
        var stored = new StoredResponse(201, "/api/v1/users/1", "application/json", [1, 2, 3]);

        await store.ClaimAsync(scope, "hash");
        await store.CompleteAsync(scope, stored);

        (await store.ClaimAsync(scope, "hash")).Should().BeEquivalentTo(new IdempotencyClaim.Replay(stored));
        (await store.ClaimAsync(scope, "other-hash")).Should().BeOfType<IdempotencyClaim.Mismatch>();
    }

    [Fact]
    public async Task AnExpiredKey_IsClaimedAgain()
    {
        var store = Store();
        var scope = Scope(Guid.NewGuid());
        await store.ClaimAsync(scope, "hash");
        await store.CompleteAsync(scope, new StoredResponse(201, null, null, []));

        _clock.Advance(Options.Retention - TimeSpan.FromMilliseconds(1));
        (await store.ClaimAsync(scope, "new-hash")).Should().BeOfType<IdempotencyClaim.Mismatch>();

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        (await store.ClaimAsync(scope, "new-hash")).Should().BeOfType<IdempotencyClaim.Claimed>();
    }

    [Fact]
    public async Task AnAbandonedClaim_IsTakenOverWhenItsLeaseRunsOut_AndNotBefore()
    {
        var store = Store();
        var scope = Scope(Guid.NewGuid());
        await store.ClaimAsync(scope, "hash");

        _clock.Advance(Options.Lease - TimeSpan.FromMilliseconds(1));
        (await store.ClaimAsync(scope, "hash")).Should().BeOfType<IdempotencyClaim.InProgress>();

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        (await store.ClaimAsync(scope, "hash")).Should().BeOfType<IdempotencyClaim.Claimed>();
    }

    [Fact]
    public async Task AReleasedKey_CanBeClaimedAtOnce()
    {
        var store = Store();
        var scope = Scope(Guid.NewGuid());
        await store.ClaimAsync(scope, "hash");

        await store.ReleaseAsync(scope);

        (await store.ClaimAsync(scope, "another-hash")).Should().BeOfType<IdempotencyClaim.Claimed>();
    }

    [Fact]
    public async Task DeleteExpired_RemovesOnlyExpiredKeys()
    {
        var store = Store();
        await store.ClaimAsync(Scope(Guid.NewGuid()), "old");
        _clock.Advance(TimeSpan.FromHours(23));
        await store.ClaimAsync(Scope(Guid.NewGuid()), "young");
        _clock.Advance(TimeSpan.FromHours(1));

        (await store.DeleteExpiredAsync()).Should().Be(1);
        (await store.DeleteExpiredAsync()).Should().Be(0);
    }

    private IdempotencyStore Store() =>
        new(fixture.CreateContext(), _clock, Microsoft.Extensions.Options.Options.Create(Options));

    private static IdempotencyScope Scope(Guid callerId) => new(callerId, "POST api/v{version:apiVersion}/users", "key-1");
}
