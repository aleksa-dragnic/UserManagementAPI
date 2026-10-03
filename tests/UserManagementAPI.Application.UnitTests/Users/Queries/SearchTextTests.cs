using UserManagementAPI.Application.Users.Queries.GetUsers;

namespace UserManagementAPI.Application.UnitTests.Users.Queries;

/// <summary>
/// The in-memory half of the search's folding. The database half is
/// PostgreSQL's unaccent, which the integration tests exercise.
/// </summary>
public sealed class SearchTextTests
{
    [Theory]
    [InlineData("Petrović", "petrovic")]
    [InlineData("ŠĆEPANOVIĆ", "scepanovic")]
    [InlineData("Đorđević", "dordevic")]
    [InlineData("Žarko Čolić", "zarko colic")]
    public void Fold_LowersAndRemovesDiacritics(string value, string expected)
    {
        SearchText.Fold(value).Should().Be(expected);
    }
}