namespace UserManagementAPI.Infrastructure.Identity;

/// <summary>Bound from the "LoginThrottle" configuration section (ADR 0024).</summary>
public sealed class LoginThrottleOptions
{
    public const string SectionName = "LoginThrottle";

    /// <summary>Wrong passwords in a row before sign-in for the email is refused for a while.</summary>
    public int Threshold { get; set; } = 5;

    /// <summary>The longest a refusal lasts; the delay doubles up to it.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>A run of failures older than this is forgotten, and the count starts again.</summary>
    public TimeSpan ForgetAfter { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Time between deletions of forgotten rows.</summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
}
