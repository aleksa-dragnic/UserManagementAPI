namespace UserManagementAPI.Infrastructure.Persistence.Seed;

/// <summary>
/// Bound from the "Seed" section. The password is a secret: user secrets
/// locally, an environment variable in a deployed instance, never a literal.
/// When it is absent the administrator is not seeded and a warning is logged.
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string AdministratorEmail { get; set; } = "admin@umapi.local";

    public string AdministratorPassword { get; set; } = string.Empty;
}