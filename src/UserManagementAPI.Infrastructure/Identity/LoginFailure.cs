namespace UserManagementAPI.Infrastructure.Identity;

/// <summary>
/// One row in login_failures: wrong passwords in a row for one email (ADR 0024).
/// Written only by <see cref="LoginThrottle"/> with SQL of its own, so the
/// counter is atomic in the database; the entity puts the table in the model
/// and its migration. Keyed by a hash of the email, known or not, so the table
/// holds no addresses and treats an unknown one exactly like a known one.
/// </summary>
public sealed class LoginFailure
{
    /// <summary>Required by EF Core.</summary>
    private LoginFailure()
    {
    }

    /// <summary>SHA-256 of the trimmed, lower-cased email, hex.</summary>
    public string EmailHash { get; private set; } = null!;

    public int ConsecutiveFailures { get; private set; }

    public DateTime LastFailureAtUtc { get; private set; }

    public DateTime? BlockedUntilUtc { get; private set; }
}
