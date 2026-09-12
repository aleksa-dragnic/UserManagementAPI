using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// GET /api/v1/users against thirteen users: the seeded administrator plus
/// user01..user12, of whom 01–04 share the last name Petrović and every third
/// one is locked.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class UserCollectionTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const int SeededUsers = 12;

    private const int TotalUsers = SeededUsers + 1;

    private ApiFactory _factory = null!;

    private HttpClient _administrator = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture);
        await _factory.ResetAndSeedAsync();
        await SeedUsersAsync();
        _administrator = await _factory.CreateAdministratorClientAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task PagesTheCollection_AndReportsItInTheXPaginationHeader()
    {
        var (users, pagination) = await GetAsync("?pageNumber=2&pageSize=5");

        users.Select(user => user.Email).Should().Equal(
            "user05@example.com", "user06@example.com", "user07@example.com", "user08@example.com", "user09@example.com");
        pagination.Should().Be(new Pagination(2, 3, 5, TotalUsers, HasPrevious: true, HasNext: true));
    }

    [Fact]
    public async Task ClampsThePageSize_ToTheMaximum()
    {
        var (users, pagination) = await GetAsync("?pageSize=500");

        users.Should().HaveCount(TotalUsers);
        pagination.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task AnOutOfRangePage_IsEmpty_WithCorrectMetadata()
    {
        var (users, pagination) = await GetAsync("?pageNumber=99&pageSize=5");

        users.Should().BeEmpty();
        pagination.Should().Be(new Pagination(99, 3, 5, TotalUsers, HasPrevious: true, HasNext: false));
    }

    [Fact]
    public async Task Search_MatchesOnEmailAndOnName_IgnoringCase()
    {
        var (byName, _) = await GetAsync("?searchTerm=PETROVI");
        var (byEmail, _) = await GetAsync("?searchTerm=User12");

        byName.Should().HaveCount(4).And.OnlyContain(user => user.LastName == "Petrović");
        byEmail.Select(user => user.Email).Should().Equal("user12@example.com");
    }

    [Fact]
    public async Task Filter_ReturnsOnlyTheRequestedStatus()
    {
        var (users, pagination) = await GetAsync("?status=locked");

        users.Should().HaveCount(4).And.OnlyContain(user => user.Status == UserStatus.Locked.Name);
        pagination.TotalCount.Should().Be(4);
    }

    [Fact]
    public async Task Sort_FollowsAWhitelistedField_AndIgnoresAnUnknownOne()
    {
        var (byFirstName, _) = await GetAsync("?orderBy=firstName%20desc&pageSize=2");
        var (byUnknown, _) = await GetAsync("?orderBy=password_hash%20desc&pageSize=2");

        byFirstName.Select(user => user.FirstName).Should().Equal("System", "First12");
        byUnknown.Select(user => user.Email).Should().Equal(DatabaseFixture.AdministratorEmail, "user01@example.com");
    }

    [Fact]
    public async Task AnUnknownStatus_IsA422_NamingTheField()
    {
        var response = await _administrator.GetAsync("/api/v1/users?status=banned");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").EnumerateObject()
            .Select(property => property.Name).Should().Contain("Status");
    }

    [Fact]
    public async Task EveryUserAppearsExactlyOnce_AcrossThePages_EvenWhenTheSortKeyTies()
    {
        var ids = new List<Guid>();

        for (var page = 1; page <= 3; page++)
        {
            var (users, _) = await GetAsync($"?orderBy=status&pageSize=5&pageNumber={page}");
            ids.AddRange(users.Select(user => user.Id));
        }

        ids.Should().HaveCount(TotalUsers).And.OnlyHaveUniqueItems();
    }

    private async Task<(List<UserResponse> Users, Pagination Pagination)> GetAsync(string query)
    {
        var response = await _administrator.GetAsync($"/api/v1/users{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await response.Content.ReadFromJsonAsync<List<UserResponse>>();
        var header = response.Headers.GetValues("X-Pagination").Single();
        var pagination = JsonSerializer.Deserialize<Pagination>(header, JsonSerializerOptions.Web);

        return (users!, pagination!);
    }

    private async Task SeedUsersAsync()
    {
        await using var context = fixture.CreateContext();

        for (var i = 1; i <= SeededUsers; i++)
        {
            var user = User.Register(
                Email.Create($"user{i:00}@example.com").Value,
                PersonName.Create($"First{i:00}", i <= 4 ? "Petrović" : $"Last{i:00}").Value,
                PasswordHash.Create("$argon2id$v=19$m=8192,t=1,p=1$c2FsdA==$aGFzaA==").Value).Value;

            if (i % 3 == 0)
            {
                user.VerifyEmail();
                user.Lock();
            }

            context.Users.Add(user);
        }

        await context.SaveChangesAsync();
    }

    private sealed record Pagination(
        int CurrentPage,
        int TotalPages,
        int PageSize,
        int TotalCount,
        bool HasPrevious,
        bool HasNext);
}