using Microsoft.Extensions.DependencyInjection;

using UserManagementAPI.Infrastructure.Identity;

namespace UserManagementAPI.IntegrationTests.Identity;

/// <summary>
/// The login throttle against PostgreSQL, with the clock in the test's hands
/// (ADR 0024): nothing for the first four failures, then a refusal of one
/// second that doubles up to fifteen minutes; a success forgets the run, so
/// does a quiet day; and two failures at once count twice.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class LoginThrottleTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string Email = "ana.petrovic@example.com";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));

    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        _provider = fixture.CreateServiceProvider(services => services.AddSingleton<TimeProvider>(_clock));
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task FourFailures_RefuseNothing_AndTheFifth_RefusesForOneSecond()
    {
        var throttle = Throttle();

        await FailAsync(throttle, times: 4);
        (await throttle.BlockedForAsync(Email)).Should().BeNull();

        await FailAsync(throttle, times: 1);
        (await throttle.BlockedForAsync(Email)).Should().Be(TimeSpan.FromSeconds(1));

        _clock.Advance(TimeSpan.FromSeconds(1));
        (await throttle.BlockedForAsync(Email)).Should().BeNull();
    }

    [Fact]
    public async Task EachFurtherFailure_DoublesTheRefusal_UpToFifteenMinutes()
    {
        var throttle = Throttle();
        await FailAsync(throttle, times: 5);

        await FailAsync(throttle, times: 1);
        (await throttle.BlockedForAsync(Email)).Should().Be(TimeSpan.FromSeconds(2));
        await FailAsync(throttle, times: 1);
        (await throttle.BlockedForAsync(Email)).Should().Be(TimeSpan.FromSeconds(4));

        await FailAsync(throttle, times: 20);
        (await throttle.BlockedForAsync(Email)).Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task TheSameEmail_TypedDifferently_IsTheSameEmail()
    {
        var throttle = Throttle();

        await FailAsync(throttle, times: 4);
        await throttle.RecordFailureAsync("  Ana.Petrovic@EXAMPLE.com ");

        (await throttle.BlockedForAsync(Email)).Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ASuccess_ForgetsTheRun()
    {
        var throttle = Throttle();
        await FailAsync(throttle, times: 5);

        await throttle.ResetAsync(Email);

        (await throttle.BlockedForAsync(Email)).Should().BeNull();
        await FailAsync(throttle, times: 4);
        (await throttle.BlockedForAsync(Email)).Should().BeNull("the count started again");
    }

    [Fact]
    public async Task ARunOlderThanADay_IsForgotten_AndCountsFromOneAgain()
    {
        var throttle = Throttle();
        await FailAsync(throttle, times: 4);

        _clock.Advance(TimeSpan.FromHours(24));
        await FailAsync(throttle, times: 1);

        (await throttle.BlockedForAsync(Email)).Should().BeNull("one failure after a quiet day is the first");
    }

    [Fact]
    public async Task TwoFailuresAtOnce_CountTwice()
    {
        // Each on its own scope and connection, as two requests on two replicas.
        await FailAsync(Throttle(), times: 3);

        await Task.WhenAll(Throttle().RecordFailureAsync(Email), Throttle().RecordFailureAsync(Email));

        (await Throttle().BlockedForAsync(Email)).Should().Be(TimeSpan.FromSeconds(1), "that was the fifth");
    }

    [Fact]
    public async Task DeleteForgotten_RemovesOnlyRunsThatAreOverAndOld()
    {
        var throttle = Throttle();
        await FailAsync(throttle, times: 2);
        _clock.Advance(TimeSpan.FromHours(23));
        await throttle.RecordFailureAsync("someone.else@example.com");
        _clock.Advance(TimeSpan.FromHours(1));

        (await throttle.DeleteForgottenAsync()).Should().Be(1);
        (await throttle.DeleteForgottenAsync()).Should().Be(0);
    }

    private LoginThrottle Throttle() => _provider.CreateScope().ServiceProvider.GetRequiredService<LoginThrottle>();

    private static async Task FailAsync(LoginThrottle throttle, int times)
    {
        for (var attempt = 0; attempt < times; attempt++)
        {
            await throttle.RecordFailureAsync(Email);
        }
    }
}
