namespace UserManagementAPI.Application.Common;

/// <summary>
/// Paging for any collection query. Out-of-range values are clamped rather than
/// rejected: a client asking for a million rows gets the maximum, a client
/// asking for page zero gets page one. A page past the end is not an error
/// either — it is an empty page with correct metadata.
/// </summary>
public abstract record RequestParameters
{
    public const int MaxPageSize = 50;

    public const int DefaultPageSize = 10;

    private readonly int _pageNumber = 1;

    private readonly int _pageSize = DefaultPageSize;

    public int PageNumber
    {
        get => _pageNumber;
        init => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize);
    }
}