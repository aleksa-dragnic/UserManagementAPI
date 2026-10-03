using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using UserManagementAPI.Application.Users.Queries.GetUsers;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Infrastructure.Persistence.Seed;

/// <summary>
/// A directory worth looking at: 130 synthetic users with the Latin letters
/// Serbian names carry, in every status, so every filter returns more than one
/// page and the search's diacritic folding meets real data.
///
/// Run once, by hand, never on startup — "seed-demo-directory" on the command
/// line, against a database that is already migrated and seeded. Idempotent: a
/// user whose email exists is skipped, and nothing outside example.org is read
/// or written.
///
/// Every user goes through the aggregate — Register, AssignRole, VerifyEmail,
/// Lock, Deactivate — so the rows are ones the API itself could have produced,
/// and the audit log records them. The password hash is well formed and
/// matches no password: these users exist to be looked at, not signed in as.
/// </summary>
public sealed class DemoDirectorySeeder(AppDbContext context, ILogger<DemoDirectorySeeder> logger)
{
    public const string EmailDomain = "example.org";

    // Argon2id with the production parameters, a fixed salt and a fixed digest
    // that no derivation produces. Verifying against it costs what a real check
    // costs, so a seeded address answers a login exactly like a wrong password.
    private const string UnusablePasswordHash =
        "$argon2id$v=19$m=19456,t=2,p=1$dW1hcGktZGVtby1kaXIhIQ==$bm8tcGFzc3dvcmQtZXZlci1wcm9kdWNlcy10aGlzISE=";

    private const int NamesPerFirstName = 5;

    private static readonly string[] FirstNames =
    [
        "Ana", "Marko", "Jelena", "Nikola", "Milica", "Stefan", "Ivana", "Luka", "Teodora",
        "Đorđe", "Katarina", "Nemanja", "Sanja", "Dušan", "Jovana", "Miloš", "Tamara", "Vuk",
        "Dragana", "Željko", "Snežana", "Aleksandar", "Mirjana", "Bojan", "Ljiljana", "Čedomir"
    ];

    private static readonly string[] LastNames =
    [
        "Petrović", "Jovanović", "Nikolić", "Đorđević", "Šćepanović", "Marković", "Ilić",
        "Stojanović", "Pavlović", "Popović", "Živković", "Kovačević", "Todorović", "Đukić",
        "Stanković", "Lukić", "Ćirić", "Radovanović", "Vuković", "Šarić", "Čolić", "Mitrović",
        "Božović", "Simić", "Tomić", "Lazić"
    ];

    /// <summary>How many users a run on an empty directory creates.</summary>
    public static int UserCount => FirstNames.Length * NamesPerFirstName;

    /// <summary>Creates the users not yet present and returns how many it created.</summary>
    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        var memberRoleId = await RoleIdAsync(RoleNames.Member, cancellationToken);
        var administratorRoleId = await RoleIdAsync(RoleNames.Administrator, cancellationToken);

        var existing = (await context.Users
                .Where(user => user.Email.Value.EndsWith("@" + EmailDomain))
                .Select(user => user.Email.Value)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var created = 0;

        for (var index = 0; index < UserCount; index++)
        {
            var (first, last) = NameAt(index);
            var email = Email.Create($"{SearchText.Fold(first)}.{SearchText.Fold(last)}@{EmailDomain}").Value;

            if (existing.Contains(email.Value))
            {
                continue;
            }

            var user = User.Register(
                email,
                PersonName.Create(first, last).Value,
                PasswordHash.Create(UnusablePasswordHash).Value).Value;

            // Roles first: a deactivated user can no longer be assigned one.
            Ensure(user.AssignRole(memberRoleId));

            if (index % 20 == 7)
            {
                Ensure(user.AssignRole(administratorRoleId));
            }

            ApplyStatus(user, index);

            context.Users.Add(user);
            created++;
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Demo directory: {Created} users created, {Skipped} already present.",
            created,
            UserCount - created);

        return created;
    }

    /// <summary>
    /// Pairs every first name with five last names and never repeats a pair:
    /// for a fixed first name the last-name index moves by five each round.
    /// </summary>
    private static (string First, string Last) NameAt(int index)
    {
        var round = index / FirstNames.Length;
        var first = FirstNames[index % FirstNames.Length];
        var last = LastNames[(index % FirstNames.Length + NamesPerFirstName * round) % LastNames.Length];

        return (first, last);
    }

    /// <summary>
    /// Two in ten Pending, two in ten Locked, one in ten Deactivated, the rest
    /// Active: every status has more than one page of ten.
    /// </summary>
    private static void ApplyStatus(User user, int index)
    {
        switch (index % 10)
        {
            case 0 or 1:
                return;

            case 2 or 3:
                Ensure(user.VerifyEmail());
                Ensure(user.Lock());
                return;

            case 4:
                Ensure(user.VerifyEmail());
                Ensure(user.Deactivate());
                return;

            default:
                Ensure(user.VerifyEmail());
                return;
        }
    }

    private async Task<Guid> RoleIdAsync(string name, CancellationToken cancellationToken)
    {
        var id = await context.Roles
            .Where(role => role.Name == name)
            .Select(role => role.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return id != Guid.Empty
            ? id
            : throw new InvalidOperationException(
                $"The role '{name}' does not exist. Seed the database first; the demo directory builds on it.");
    }

    private static void Ensure(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Seeding the demo directory broke a domain rule: {result.Error.Code}.");
        }
    }
}