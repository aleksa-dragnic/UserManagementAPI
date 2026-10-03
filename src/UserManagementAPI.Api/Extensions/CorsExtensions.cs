namespace UserManagementAPI.Api.Extensions;

/// <summary>
/// One named policy, with the allowed origins read from configuration. The
/// browser client is named by its exact origin, so a wildcard would be
/// permissiveness with no beneficiary — and ASP.NET Core refuses a wildcard
/// together with credentials, which the refresh cookie needs (ADR 0019). An
/// empty list means no cross-origin request is allowed, which is the right
/// default for an API called from a server.
///
/// X-Pagination, ETag, X-Correlation-Id and the version headers are exposed,
/// because a browser cannot read a response header that is not — the paging
/// metadata, the entity tag and the id a log line carries would exist and be
/// invisible.
/// </summary>
public static class CorsExtensions
{
    public const string PolicyName = "ApiCors";

    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

        services.AddCors(cors => cors.AddPolicy(PolicyName, policy =>
        {
            policy
                .WithOrigins(options.AllowedOrigins)
                .WithHeaders("Authorization", "Content-Type", "Accept", "If-None-Match")
                .WithMethods("GET", "HEAD", "POST", "PUT", "DELETE", "OPTIONS")
                .WithExposedHeaders(
                    "X-Pagination",
                    "ETag",
                    "Retry-After",
                    "X-Correlation-Id",
                    "api-supported-versions",
                    "api-deprecated-versions");

            if (options.AllowCredentials && options.AllowedOrigins.Length > 0)
            {
                policy.AllowCredentials();
            }
        }));

        return services;
    }
}