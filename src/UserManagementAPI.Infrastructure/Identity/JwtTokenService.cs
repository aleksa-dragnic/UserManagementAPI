using System.Security.Claims;
using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Infrastructure.Identity;

/// <summary>
/// Issues HS256 access tokens. Claims: subject, email, a unique id, and one
/// "permission" claim per code. Roles are deliberately absent — the policies in
/// PR16 check permissions, and putting role names in the token would tempt
/// something to check them.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        if (_options.SigningKey.Length < JwtOptions.MinimumSigningKeyLength)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey must be at least {JwtOptions.MinimumSigningKeyLength} characters. " +
                "Set it with dotnet user-secrets locally or as Jwt__SigningKey in a deployed environment.");
        }

        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public IssuedToken CreateAccessToken(User user, IReadOnlyCollection<string> permissions)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(_options.AccessTokenLifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(permissions.Select(code => new Claim(AuthClaimTypes.Permission, code)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = _signingCredentials
        };

        return new IssuedToken(_handler.CreateToken(descriptor), expiresAt);
    }
}