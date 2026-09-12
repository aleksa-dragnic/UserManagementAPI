namespace UserManagementAPI.Api.Extensions;

/// <summary>
/// Bound from the "RateLimiting" section. In configuration rather than in code
/// because the right numbers depend on where the API runs, and a deployment
/// behind a single office IP needs different ones from a public instance.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitWindow Auth { get; set; } = new(10, TimeSpan.FromMinutes(1));

    public RateLimitWindow Read { get; set; } = new(100, TimeSpan.FromMinutes(1));

    public RateLimitWindow Write { get; set; } = new(30, TimeSpan.FromMinutes(1));

    public int DefaultRetryAfterSeconds { get; set; } = 60;
}

public sealed record RateLimitWindow(int PermitLimit, TimeSpan Window)
{
    public RateLimitWindow() : this(60, TimeSpan.FromMinutes(1))
    {
    }
}