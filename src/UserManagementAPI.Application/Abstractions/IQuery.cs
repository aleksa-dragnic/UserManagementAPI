namespace UserManagementAPI.Application.Abstractions;

/// <summary>
/// A request to read state. Queries never go through the domain model or a
/// repository — their handlers project straight to DTOs (M5).
/// </summary>
public interface IQuery<TResponse>;