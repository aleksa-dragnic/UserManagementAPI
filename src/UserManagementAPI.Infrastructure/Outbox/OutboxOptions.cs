namespace UserManagementAPI.Infrastructure.Outbox;

/// <summary>Bound from the "Outbox" configuration section; defaults are sensible for development.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Messages taken per poll.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>Time between polls.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>After this many failures a message is abandoned and left for a human.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Delay after the first failure; doubles on each subsequent one.</summary>
    public TimeSpan BaseBackoff { get; set; } = TimeSpan.FromSeconds(10);
}