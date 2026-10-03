using Riok.Mapperly.Abstractions;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Application.Auth;
using UserManagementAPI.Application.Auth.Commands.Login;

namespace UserManagementAPI.Api.Mapping;

[Mapper]
public static partial class AuthRequestMapper
{
    public static partial LoginCommand ToCommand(this LoginRequest request);

    // The refresh token and its expiry go into the cookie, not the body
    // (ADR 0019). Ignored by name so an unmapped member stays a build error
    // everywhere else.
    [MapperIgnoreSource(nameof(AuthTokens.RefreshToken))]
    [MapperIgnoreSource(nameof(AuthTokens.RefreshTokenExpiresAtUtc))]
    public static partial TokenResponse ToResponse(this AuthTokens tokens);
}