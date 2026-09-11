namespace UserManagementAPI.Api.Contracts.V1;

/// <summary>Body of the 201 from POST /api/v1/users. The Location header carries the same id.</summary>
public sealed record UserCreatedResponse(Guid Id);