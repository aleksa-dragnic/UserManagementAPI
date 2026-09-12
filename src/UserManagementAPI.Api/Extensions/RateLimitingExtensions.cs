using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace UserManagementAPI.Api.Extensions;

/// <summary>
/// Three policies, because the endpoints differ in what abuse of them costs.
///
/// Login and refresh are the endpoints worth brute-forcing, and the caller is
/// by definition not authenticated yet, so they are limited strictly and keyed
/// by IP. Reads and writes are limited by authenticated user id — a shared
/// office NAT would otherwise make one user's traffic count against everyone
/// behind it — and an unauthenticated caller falls back to the IP, which is all
/// that is known about them.
///
/// A rejection answers 429 with Retry-After and problem details, so a client is
/// told how to behave rather than just refused.
/// </summary>
public static class RateLimitingExtensions
{
    public const string AuthPolicy = "auth";

    public const string ReadPolicy = "read";

    public const string WritePolicy = "write";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            AddFixedWindow(limiter, AuthPolicy, options.Auth, ClientIp);
            AddFixedWindow(limiter, ReadPolicy, options.Read, UserOrClientIp);
            AddFixedWindow(limiter, WritePolicy, options.Write, UserOrClientIp);

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var metadata)
                    ? metadata
                    : TimeSpan.FromSeconds(options.DefaultRetryAfterSeconds);

                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/problem+json";

                var problem = new ProblemDetails
                {
                    Type = "https://datatracker.ietf.org/doc/html/rfc6585#section-4",
                    Title = "Too Many Requests",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = "The request was rate limited. Retry after the interval in the Retry-After header.",
                    Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}"
                };

                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(problem, JsonSerializerOptions.Web),
                    cancellationToken);
            };
        });

        return services;
    }

    private static void AddFixedWindow(
        RateLimiterOptions limiter,
        string policyName,
        RateLimitWindow window,
        Func<HttpContext, string> partitionKey) =>
        limiter.AddPolicy(policyName, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            $"{policyName}:{partitionKey(httpContext)}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = window.PermitLimit,
                Window = window.Window,
                QueueLimit = 0
            }));

    private static string UserOrClientIp(HttpContext httpContext) =>
        httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst("sub")?.Value ?? "authenticated"
            : ClientIp(httpContext);

    private static string ClientIp(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}