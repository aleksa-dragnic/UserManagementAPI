namespace UserManagementAPI.Application.Abstractions;

/// <summary>
/// Two requests changed the same row and the second one lost. Raised by the
/// persistence layer, translated to 409 by the Api: the client's information
/// was stale, and the fix is to read again and retry.
/// </summary>
public sealed class ConcurrencyConflictException(string message, Exception innerException)
    : Exception(message, innerException);