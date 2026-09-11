using System.Text.Json;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.Infrastructure.Outbox;

/// <summary>
/// Serializes the event and adds it to the same DbContext that is mid-save.
/// Domain event handlers run inside SavingChangesAsync, before EF detects
/// changes, so a message added here is written by the very SaveChanges that
/// triggered the event — same statement batch, same transaction.
/// </summary>
internal sealed class OutboxWriter(AppDbContext context) : IOutboxWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var eventType = domainEvent.GetType();

        var message = new OutboxMessage(
            Guid.NewGuid(),
            eventType.FullName!,
            JsonSerializer.Serialize(domainEvent, eventType, SerializerOptions),
            domainEvent.OccurredOnUtc);

        await context.OutboxMessages.AddAsync(message, cancellationToken);
    }
}