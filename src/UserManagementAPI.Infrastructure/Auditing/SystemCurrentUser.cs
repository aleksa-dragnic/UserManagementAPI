using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Infrastructure.Auditing;

/// <summary>
/// The actor when there is no request: the seeder at startup, the outbox
/// processor, a test through the wiring. Registered with TryAdd, so the Api's
/// HTTP-aware implementation replaces it whenever one is registered first.
/// </summary>
internal sealed class SystemCurrentUser : ICurrentUser
{
    public Guid? UserId => null;

    public bool IsAuthenticated => false;
}