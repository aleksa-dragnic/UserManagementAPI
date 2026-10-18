using System.Diagnostics.Metrics;

namespace UserManagementAPI.Application.Auth;

/// <summary>
/// The two numbers about authentication worth watching in production: how many
/// logins succeed and how many are refused, and how often a refresh token is
/// presented a second time — which is either a client bug or a stolen token
/// (ADR 0009). Recorded through System.Diagnostics.Metrics, so the Application
/// layer depends on no telemetry library; OpenTelemetry subscribes to the meter.
/// </summary>
public static class AuthMetrics
{
    public const string MeterName = "UserManagementAPI";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Logins = Meter.CreateCounter<long>(
        "umapi.auth.logins",
        unit: "{login}",
        description: "Login attempts, by outcome and, for a refusal, by error code.");

    private static readonly Counter<long> RefreshReuse = Meter.CreateCounter<long>(
        "umapi.auth.refresh_reuse_detected",
        unit: "{token}",
        description: "Refresh tokens presented after they were already exchanged.");

    public static void LoginSucceeded() =>
        Logins.Add(1, new KeyValuePair<string, object?>("outcome", "succeeded"));

    public static void LoginRefused(string errorCode) =>
        Logins.Add(
            1,
            new KeyValuePair<string, object?>("outcome", "refused"),
            new KeyValuePair<string, object?>("error_code", errorCode));

    public static void RefreshReuseDetected() => RefreshReuse.Add(1);
}
