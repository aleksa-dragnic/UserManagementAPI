using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Infrastructure.Queries.Users;

namespace UserManagementAPI.IntegrationTests.Queries;

/// <summary>
/// The projection has to become the SELECT list. If it were applied after
/// materialisation, the query would read every column — the password hash
/// included — and this test would see them.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class UserProjectionTests(DatabaseFixture fixture)
{
    [Fact]
    public void TheUserProjection_SelectsOnlyTheColumnsTheDtoNeeds()
    {
        using var context = fixture.CreateContext();

        var sql = context.Users.AsNoTracking().Select(UserReadModels.ToDto).ToQueryString();

        sql.Should().Contain("email").And.Contain("name_first").And.Contain("name_last").And.Contain("status_id");
        sql.Should().NotContain("password_hash").And.NotContain("created_at_utc");
    }
}