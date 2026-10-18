using System.Globalization;

using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Api.Extensions;

/// <summary>
/// Reads the version a client sends back in If-Match (ADR 0021). The value is
/// the user's <c>version</c> field in quotes, compared strongly, so a weak tag
/// is refused. A missing header, or <c>*</c>, asks for no check.
/// </summary>
public static class IfMatchHeader
{
    public static readonly Error Invalid = new(
        "Request.InvalidIfMatch",
        "If-Match must be one quoted version as the user's version field gives it, for example \"1234\".");

    public static bool TryRead(string? value, out uint? version)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(value) || value.Trim() == "*")
        {
            return true;
        }

        var trimmed = value.Trim();

        if (trimmed.Length < 3 ||
            trimmed[0] != '"' ||
            trimmed[^1] != '"' ||
            !uint.TryParse(trimmed.AsSpan(1, trimmed.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        version = parsed;
        return true;
    }
}
