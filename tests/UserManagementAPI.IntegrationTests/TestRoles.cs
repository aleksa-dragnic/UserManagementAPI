using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Domain.Roles;

namespace UserManagementAPI.IntegrationTests;

/// <summary>
/// Every user is registered with a role, and user_roles.role_id is a foreign
/// key, so a test that persists a user needs a real role first. The Member
/// role is reused when the seeder already created it and created otherwise,
/// in a context of its own so it is committed before the user is added.
/// </summary>
internal static class TestRoles
{
    public static async Task<Guid> MemberIdAsync(DatabaseFixture fixture)
    {
        await using var context = fixture.CreateContext();

        var existing = await context.Roles
            .Where(role => role.Name == RoleNames.Member)
            .Select(role => role.Id)
            .SingleOrDefaultAsync();

        if (existing != Guid.Empty)
        {
            return existing;
        }

        var member = Role.Create(RoleNames.Member).Value;
        context.Roles.Add(member);
        await context.SaveChangesAsync();

        return member.Id;
    }
}
