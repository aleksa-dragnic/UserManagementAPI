using Microsoft.EntityFrameworkCore;

namespace UserManagementAPI.IntegrationTests.Persistence;

/// <summary>
/// Migrations run by hand against a deployed database (docs/DATABASE.md), so a
/// change to the model without its migration would surface only after a
/// deploy, as a query against a column that does not exist. This fails the
/// build instead.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class MigrationsTests(DatabaseFixture fixture)
{
    [Fact]
    public void TheModel_HasNoChangeThatIsMissingFromTheMigrations()
    {
        using var context = fixture.CreateContext();

        context.Database.HasPendingModelChanges().Should().BeFalse();
    }
}
