using Riok.Mapperly.Abstractions;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Application.Users.Dtos;

namespace UserManagementAPI.Api.Mapping;

/// <summary>
/// Read model to response contract (ADR 0011, 0014). Today the shapes match;
/// the mapper is where they are allowed to drift apart when a version needs it.
/// </summary>
[Mapper]
public static partial class UserResponseMapper
{
    public static partial UserResponse ToResponse(this UserDto user);

    public static partial UserDetailsResponse ToResponse(this UserDetailsDto user);
}