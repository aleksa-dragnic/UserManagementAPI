namespace UserManagementAPI.Application.Common;

/// <summary>
/// One page of a collection and its metadata. Built by the query handler in
/// Infrastructure, which runs the count and the page query (ADR 0016).
/// </summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, MetaData MetaData);