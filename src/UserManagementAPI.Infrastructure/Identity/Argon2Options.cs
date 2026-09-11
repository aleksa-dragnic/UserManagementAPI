namespace UserManagementAPI.Infrastructure.Identity;

/// <summary>
/// Argon2id cost parameters, bound from the "Argon2" section. Configuration
/// rather than literals so they can be raised as hardware improves without a
/// code change; the parameters used are stored inside each hash, so raising
/// them never breaks an existing password.
///
/// Defaults are the OWASP recommendation for Argon2id: 19 MiB of memory, 2
/// iterations, 1 lane.
/// </summary>
public sealed class Argon2Options
{
    public const string SectionName = "Argon2";

    public int MemorySizeKb { get; set; } = 19_456;

    public int Iterations { get; set; } = 2;

    public int DegreeOfParallelism { get; set; } = 1;
}