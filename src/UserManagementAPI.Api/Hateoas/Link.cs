namespace UserManagementAPI.Api.Hateoas;

/// <summary>Where a client can go next: the address, what it means, and the method to use.</summary>
public sealed record Link(string Href, string Rel, string Method);