using System.Buffers.Text;
using System.Security.Cryptography;

namespace UserManagementAPI.Api.Caching;

/// <summary>
/// A strong entity tag from the bytes of a representation: SHA-256, base64url,
/// quoted. Stable by construction — the same bytes always give the same tag, and
/// nothing else (no timestamp, no request id) goes in.
/// </summary>
public static class ETagGenerator
{
    public static string Generate(ReadOnlySpan<byte> representation) =>
        $"\"{Base64Url.EncodeToString(SHA256.HashData(representation))}\"";
}