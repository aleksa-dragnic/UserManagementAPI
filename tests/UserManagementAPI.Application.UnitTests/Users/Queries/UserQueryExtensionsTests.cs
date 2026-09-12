using UserManagementAPI.Application.Users.Queries.GetUsers;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.UnitTests.Users.Queries;

/// <summary>
/// The filter, search and sort rules in memory. The same expressions run as SQL
/// in the handler; the integration tests cover that half.
/// </summary>
public sealed class UserQueryExtensionsTests
{
    [Fact]
    public void Search_MatchesOnEmail()
    {
        var result = Sample().Search("PETROVIC.RS").ToList();

        result.Select(user => user.Email.Value).Should().Equal("jelena@petrovic.rs");
    }

    [Fact]
    public void Search_MatchesOnFirstOrLastName_IgnoringCase()
    {
        Sample().Search("PETROVIĆ").Select(user => user.Name.First).Should().BeEquivalentTo("Ana", "Ivana");
        Sample().Search("marko").Select(user => user.Name.First).Should().Equal("Marko");
    }

    [Fact]
    public void Search_KeepsEveryone_WhenTheTermIsBlank()
    {
        Sample().Search("   ").Should().HaveCount(4);
    }

    [Fact]
    public void Filter_KeepsOnlyTheRequestedStatus_IgnoringCase()
    {
        Sample().Filter("locked").Select(user => user.Name.First).Should().Equal("Jelena");
        Sample().Filter("ACTIVE").Select(user => user.Name.First).Should().Equal("Marko");
    }

    [Fact]
    public void Sort_OrdersByAWhitelistedField_InTheRequestedDirection()
    {
        var result = Sample().Sort("firstName desc");

        result.Select(user => user.Name.First).Should().Equal("Marko", "Jelena", "Ivana", "Ana");
    }

    [Fact]
    public void Sort_FallsBackToEmailAscending_ForAnUnknownField()
    {
        var result = Sample().Sort("passwordHash desc");

        result.Select(user => user.Email.Value).Should().Equal(
            "ana.petrovic@example.com",
            "ivana@example.com",
            "jelena@petrovic.rs",
            "marko.jovanovic@example.com");
    }

    [Fact]
    public void Sort_AppliesEveryClauseInOrder()
    {
        var result = Sample().Sort("lastName, firstName desc");

        result.Select(user => user.Name.First).Should().Equal("Marko", "Jelena", "Ivana", "Ana");
    }

    private static IQueryable<User> Sample()
    {
        var ana = NewUser("ana.petrovic@example.com", "Ana", "Petrović");

        var ivana = NewUser("ivana@example.com", "Ivana", "Petrović");

        var marko = NewUser("marko.jovanovic@example.com", "Marko", "Jovanović");
        marko.VerifyEmail();

        var jelena = NewUser("jelena@petrovic.rs", "Jelena", "Nikolić");
        jelena.VerifyEmail();
        jelena.Lock();

        return new[] { ana, ivana, marko, jelena }.AsQueryable();
    }

    private static User NewUser(string email, string first, string last) =>
        User.Register(TestUsers.Email(email), TestUsers.Name(first, last), TestUsers.Hash()).Value;
}