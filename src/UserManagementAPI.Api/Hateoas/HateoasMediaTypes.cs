namespace UserManagementAPI.Api.Hateoas;

/// <summary>
/// The vendor media type a client sends in Accept to ask for links alongside
/// the data. Plain application/json never carries them (ADR 0013).
/// </summary>
public static class HateoasMediaTypes
{
    public const string Hateoas = "application/vnd.umapi.hateoas+json";

    /// <summary>HttpContext.Items key set by ValidateMediaTypeAttribute when the client asked for links.</summary>
    internal const string RequestedItemKey = "UmApi.HateoasRequested";

    public static bool WantsHateoas(this HttpContext httpContext) =>
        httpContext.Items.TryGetValue(RequestedItemKey, out var requested) && requested is true;
}