using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Auth;
using UserManagementAPI.Application.Auth.Commands.Login;
using UserManagementAPI.Application.UnitTests.Users;
using UserManagementAPI.Domain.Auth;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.UnitTests.Auth;

/// <summary>
/// The login counter is recorded where the outcome is decided. Measurements are
/// read with a MeterListener; the assertions look for the specific tags this
/// test causes, so tests running in parallel cannot satisfy them by accident.
/// </summary>
public sealed class AuthMetricsTests : IDisposable
{
    private readonly ConcurrentBag<(string Instrument, long Value, Dictionary<string, object?> Tags)> _measurements = [];
    private readonly MeterListener _listener = new();

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public AuthMetricsTests()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AuthMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var copy = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                copy[tag.Key] = tag.Value;
            }

            _measurements.Add((instrument.Name, value, copy));
        });
        _listener.Start();
    }

    [Fact]
    public async Task ARefusedLogin_IsCounted_WithTheErrorCodeThatRefusedIt()
    {
        var user = TestUsers.Locked();
        _userRepository.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await Handler().HandleAsync(new LoginCommand(user.Email.Value, "correct horse battery staple"));

        _measurements.Should().Contain(measurement =>
            measurement.Instrument == "umapi.auth.logins" &&
            Equals(measurement.Tags["outcome"], "refused") &&
            Equals(measurement.Tags["error_code"], "Auth.AccountLocked"));
    }

    public void Dispose() => _listener.Dispose();

    private LoginCommandHandler Handler() => new(
        _userRepository,
        _passwordHasher,
        new TokenIssuer(
            Substitute.For<IPermissionLookup>(),
            Substitute.For<ITokenService>(),
            Substitute.For<IRefreshTokenRepository>()),
        _unitOfWork);
}
