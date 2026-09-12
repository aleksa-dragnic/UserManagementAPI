using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Auth.Commands.Login;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.Infrastructure.Persistence;
using UserManagementAPI.Infrastructure.Persistence.Seed;

namespace UserManagementAPI.IntegrationTests.Auditing;

[Collection(DatabaseCollection.Name)]
public sealed class AuditInterceptorTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string Hash = "$argon2id$v=19$m=8192,t=1,p=1$c2FsdHNhbHRzYWx0c2FsdA==$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYXNoaGFzaGhhc2g=";

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AnAuthenticatedWrite_RecordsTheActingUser()
    {
        var actorId = Guid.NewGuid();
        await using var provider = fixture.CreateServiceProvider(services =>
            services.AddScoped<ICurrentUser>(_ => new FakeCurrentUser(actorId)));

        var user = await RegisterAsync(provider, "audited@example.com");

        await using var context = fixture.CreateContext();
        var entry = context.AuditLog.Should().ContainSingle(e => e.Entity == nameof(User)).Subject;

        entry.ActorId.Should().Be(actorId);
        entry.Action.Should().Be("Created");
        entry.EntityId.Should().Be(user.Id.ToString());
        // jsonb stores a normalised form ({"changedFields": []}), so compare
        // the parsed document rather than the text.
        using var payload = JsonDocument.Parse(entry.Payload);
        payload.RootElement.GetProperty("changedFields").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ASystemWrite_RecordsNoActor_InsteadOfFailing()
    {
        await using var provider = fixture.CreateServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            await ActivatorUtilities.CreateInstance<DatabaseSeeder>(scope.ServiceProvider).SeedAsync();
        }

        await using var context = fixture.CreateContext();
        var entries = await context.AuditLog.ToListAsync();

        entries.Should().NotBeEmpty();
        entries.Should().OnlyContain(entry => entry.ActorId == null);
        entries.Should().Contain(entry => entry.Entity == nameof(User) && entry.Action == "Created");
        entries.Should().Contain(entry => entry.Entity == "Role" && entry.Action == "Created");
    }

    [Fact]
    public async Task AChangeToAnOwnedValueObject_IsAttributedToTheOwner_ByFieldNameOnly()
    {
        await using var provider = fixture.CreateServiceProvider();
        var user = await RegisterAsync(provider, "before@example.com");

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tracked = await context.Users.SingleAsync(candidate => candidate.Id == user.Id);

            tracked.ChangeEmail(Email.Create("after@example.com").Value);
            await context.SaveChangesAsync();
        }

        await using var verify = fixture.CreateContext();
        var update = await verify.AuditLog.SingleAsync(entry => entry.Action == "Updated");

        update.Entity.Should().Be(nameof(User));
        update.EntityId.Should().Be(user.Id.ToString());
        update.Payload.Should().Contain("Email.Value").And.Contain("UpdatedAtUtc");
        update.Payload.Should().NotContain("after@example.com").And.NotContain("before@example.com");
        verify.AuditLog.Should().NotContain(entry => entry.Entity == "Email");
    }

    [Fact]
    public async Task AnUpdateThatChangesNothing_WritesNoAuditEntry()
    {
        await using var provider = fixture.CreateServiceProvider();
        var user = await RegisterAsync(provider, "unchanged@example.com");

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tracked = await context.Users.SingleAsync(candidate => candidate.Id == user.Id);

            tracked.ChangeEmail(Email.Create("UNCHANGED@example.com").Value);
            tracked.ChangeName(PersonName.Create("Ana", "Petrović").Value);
            await context.SaveChangesAsync();
        }

        await using var verify = fixture.CreateContext();
        verify.AuditLog.Should().NotContain(entry => entry.Action == "Updated");
    }

    [Fact]
    public async Task NoAuditPayload_ContainsAPasswordHashOrAToken_AndTokensAreNotAudited()
    {
        await using var provider = fixture.CreateServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            await ActivatorUtilities.CreateInstance<DatabaseSeeder>(scope.ServiceProvider).SeedAsync();
        }

        string refreshToken;
        await using (var scope = provider.CreateAsyncScope())
        {
            var login = await scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(
                new LoginCommand(DatabaseFixture.AdministratorEmail, DatabaseFixture.AdministratorPassword));
            login.IsSuccess.Should().BeTrue();
            refreshToken = login.Value.RefreshToken;
        }

        await using var context = fixture.CreateContext();
        var administrator = await context.Users.SingleAsync();
        var entries = await context.AuditLog.ToListAsync();

        entries.Should().NotBeEmpty();
        entries.Should().NotContain(entry => entry.Entity == "RefreshToken");
        entries.Should().NotContain(entry => entry.Entity == "OutboxMessage");

        foreach (var entry in entries)
        {
            entry.Payload.Should().NotContain(administrator.PasswordHash.Value);
            entry.Payload.Should().NotContain(DatabaseFixture.AdministratorPassword);
            entry.Payload.Should().NotContain(refreshToken);
            entry.Payload.Should().NotContain(DatabaseFixture.AdministratorEmail);
        }
    }

    private static async Task<User> RegisterAsync(ServiceProvider provider, string email)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = User.Register(
            Email.Create(email).Value,
            PersonName.Create("Ana", "Petrović").Value,
            PasswordHash.Create(Hash).Value).Value;

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId => userId;

        public bool IsAuthenticated => true;
    }
}