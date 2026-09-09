using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Domain.UnitTests.Users;

public class PersonNameTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Create_Fails_WhenFirstIsMissing(string? first)
    {
        var result = PersonName.Create(first, "Petrović");

        result.Error.Should().Be(PersonName.FirstEmpty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Create_Fails_WhenLastIsMissing(string? last)
    {
        var result = PersonName.Create("Ana", last);

        result.Error.Should().Be(PersonName.LastEmpty);
    }

    [Fact]
    public void Create_Fails_WhenAPartExceedsMaxLength()
    {
        var result = PersonName.Create(new string('a', PersonName.MaxPartLength + 1), "Petrović");

        result.Error.Should().Be(PersonName.TooLong);
    }

    [Fact]
    public void Create_TrimsBothParts()
    {
        var result = PersonName.Create("  Ana  ", "  Petrović  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.First.Should().Be("Ana");
        result.Value.Last.Should().Be("Petrović");
        result.Value.FullName.Should().Be("Ana Petrović");
    }
}

public class PasswordHashTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Fails_WhenValueIsMissing(string? value)
    {
        var result = PasswordHash.Create(value);

        result.Error.Should().Be(PasswordHash.Empty);
    }

    [Fact]
    public void Create_KeepsTheHashVerbatim()
    {
        const string hash = "$argon2id$v=19$m=65536,t=3,p=1$c2FsdA$aGFzaA";

        var result = PasswordHash.Create(hash);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(hash);
    }
}