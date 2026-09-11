using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.Abstractions;

/// <summary>
/// Records the intent to publish an event outside the service. The message is
/// written through the same DbContext the current write uses, so it joins the
/// ambient transaction: if the write rolls back, the message was never there.
/// A background worker publishes it afterwards (ADR 0008).
/// </summary>
public interface IOutboxWriter
{
    Task WriteAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}