using UserManagementAPI.Application.Users.Dtos;

using V2Contracts = UserManagementAPI.Api.Contracts.V2;

namespace UserManagementAPI.Api.Mapping;

/// <summary>
/// Hand-written, unlike the other mappers: the one member that differs is a
/// composition of two source members, and a one-line method says that more
/// plainly than a Mapperly attribute pointing at a helper would.
/// </summary>
public static class V2UserResponseMapper
{
    public static V2Contracts.UserResponse ToV2Response(this UserDetailsDto user) =>
        new(user.Id, user.Email, $"{user.FirstName} {user.LastName}", user.Status);
}