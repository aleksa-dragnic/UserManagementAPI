using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.Common;

/// <summary>
/// The failure a command produces when its shape is wrong. It is one Error, so
/// a failed Result still carries a single code the HTTP layer maps to 422, and
/// it carries the per-property errors so the response can list them. Each inner
/// error's code is "Validation.&lt;PropertyName&gt;".
/// </summary>
public sealed record ValidationError(IReadOnlyList<Error> Errors)
    : Error(GeneralCode, "One or more validation errors occurred.")
{
    public const string CodePrefix = "Validation.";

    public const string GeneralCode = "Validation.General";
}