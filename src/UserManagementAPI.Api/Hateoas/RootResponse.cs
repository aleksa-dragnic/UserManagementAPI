namespace UserManagementAPI.Api.Hateoas;

/// <summary>The entry point: nothing but links to the top-level resources.</summary>
public sealed record RootResponse(IReadOnlyList<Link> Links);