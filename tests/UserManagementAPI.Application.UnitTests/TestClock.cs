namespace UserManagementAPI.Application.UnitTests;

/// <summary>
/// The one instant the tests in this project give the domain. The domain takes
/// time as a value (#84), so a test states it rather than reading a clock, and
/// a timestamp or an expiry can be asserted exactly.
/// </summary>
internal static class TestClock
{
    public static readonly DateTime Now = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);
}
