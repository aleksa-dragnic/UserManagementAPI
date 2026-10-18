namespace UserManagementAPI.Api.Errors;

/// <summary>
/// The problem "type" URI for each status, in one place. The application's own
/// problems and the ones the framework writes (status code pages, model
/// binding) used to point at two different copies of RFC 9110; every problem
/// now points at the same one.
/// </summary>
internal static class ProblemTypes
{
    private const string Rfc9110 = "https://datatracker.ietf.org/doc/html/rfc9110#section-";

    public static string? For(int status) => status switch
    {
        StatusCodes.Status400BadRequest => Rfc9110 + "15.5.1",
        StatusCodes.Status401Unauthorized => Rfc9110 + "15.5.2",
        StatusCodes.Status403Forbidden => Rfc9110 + "15.5.4",
        StatusCodes.Status404NotFound => Rfc9110 + "15.5.5",
        StatusCodes.Status405MethodNotAllowed => Rfc9110 + "15.5.6",
        StatusCodes.Status406NotAcceptable => Rfc9110 + "15.5.7",
        StatusCodes.Status409Conflict => Rfc9110 + "15.5.10",
        StatusCodes.Status412PreconditionFailed => Rfc9110 + "15.5.13",
        StatusCodes.Status413PayloadTooLarge => Rfc9110 + "15.5.14",
        StatusCodes.Status415UnsupportedMediaType => Rfc9110 + "15.5.16",
        StatusCodes.Status422UnprocessableEntity => Rfc9110 + "15.5.21",
        StatusCodes.Status429TooManyRequests => "https://datatracker.ietf.org/doc/html/rfc6585#section-4",
        StatusCodes.Status500InternalServerError => Rfc9110 + "15.6.1",
        StatusCodes.Status503ServiceUnavailable => Rfc9110 + "15.6.4",
        _ => null
    };
}
