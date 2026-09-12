namespace UserManagementAPI.Api.Extensions;

/// <summary>Bound from the "Cors" section. Empty by default: no origin is allowed until one is named.</summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];

    public bool AllowCredentials { get; set; }
}