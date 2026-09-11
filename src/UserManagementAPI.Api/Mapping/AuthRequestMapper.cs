using Riok.Mapperly.Abstractions;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Application.Auth;
using UserManagementAPI.Application.Auth.Commands.Login;

namespace UserManagementAPI.Api.Mapping;

[Mapper]
public static partial class AuthRequestMapper
{
    public static partial LoginCommand ToCommand(this LoginRequest request);

    public static partial TokenResponse ToResponse(this AuthTokens tokens);
}