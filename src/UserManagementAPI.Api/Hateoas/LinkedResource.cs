namespace UserManagementAPI.Api.Hateoas;

/// <summary>A payload and the links that apply to it.</summary>
public sealed record LinkedResource<T>(T Value, IReadOnlyList<Link> Links);