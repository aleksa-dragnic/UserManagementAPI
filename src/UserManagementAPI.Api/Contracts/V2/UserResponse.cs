namespace UserManagementAPI.Api.Contracts.V2;

/// <summary>
/// The v2 shape of a single user: one display name instead of first and last,
/// no role list. The query, the read model and the aggregate are the ones v1
/// uses — only this contract and its mapping are new (ADR 0011).
/// </summary>
public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Status);