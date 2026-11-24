namespace UserManagementAPI.Application.Abstractions;

/// <summary>
/// Slows password guessing against one account (ADR 0024). Keyed by the email
/// as typed, known or not, so the throttle cannot tell anyone which emails
/// exist. Implemented in Infrastructure; its writes commit on their own, since
/// a refused login rolls its own transaction back.
/// </summary>
public interface ILoginThrottle
{
    /// <summary>How long sign-in for this email is still refused, or null when it is not.</summary>
    Task<TimeSpan?> BlockedForAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Counts one more wrong password in a row, and refuses the next attempts for a while once there are enough.</summary>
    Task RecordFailureAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Forgets the failures: the password was right.</summary>
    Task ResetAsync(string email, CancellationToken cancellationToken = default);
}
