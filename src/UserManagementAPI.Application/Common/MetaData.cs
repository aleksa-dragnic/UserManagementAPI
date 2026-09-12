namespace UserManagementAPI.Application.Common;

/// <summary>
/// Where a page sits in its collection. The Api writes it to the X-Pagination
/// header, so the body stays a plain array a client can bind without a wrapper.
/// </summary>
public sealed record MetaData(int CurrentPage, int TotalPages, int PageSize, int TotalCount)
{
    public bool HasPrevious => CurrentPage > 1;

    public bool HasNext => CurrentPage < TotalPages;

    public static MetaData Create(int pageNumber, int pageSize, int totalCount) =>
        new(pageNumber, (int)Math.Ceiling(totalCount / (double)pageSize), pageSize, totalCount);
}