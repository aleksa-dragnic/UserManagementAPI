using Microsoft.Extensions.Options;

using UserManagementAPI.Infrastructure.Identity;

namespace UserManagementAPI.IntegrationTests.Identity;

/// <summary>
/// No database here; these live in this project because Infrastructure has no
/// unit test project of its own. Low cost parameters so the suite stays fast —
/// the algorithm is the same.
/// </summary>
public sealed class Argon2PasswordHasherTests
{
    public static Argon2PasswordHasher FastHasher() => new(Options.Create(new Argon2Options
    {
        MemorySizeKb = 8_192,
        Iterations = 1,
        DegreeOfParallelism = 1
    }));

    [Fact]
    public void HashAndVerify_RoundTrip()
    {
        var hasher = FastHasher();

        var hash = hasher.Hash("correct horse battery staple");

        hash.Should().StartWith("$argon2id$v=19$m=8192,t=1,p=1$");
        hasher.Verify("correct horse battery staple", hash).Should().BeTrue();
    }

    [Fact]
    public void TwoHashesOfTheSamePassword_Differ_BecauseTheSaltDoes()
    {
        var hasher = FastHasher();

        hasher.Hash("same password").Should().NotBe(hasher.Hash("same password"));
    }

    [Fact]
    public void Verify_Fails_ForTheWrongPassword()
    {
        var hasher = FastHasher();

        var hash = hasher.Hash("right");

        hasher.Verify("wrong", hash).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("pbkdf2-sha256$600000$c2FsdA==$aGFzaA==")]
    [InlineData("$argon2id$v=19$m=8192,t=1,p=1$not-base64$aGFzaA==")]
    [InlineData("$argon2id$v=19$m=0,t=1,p=1$c2FsdA==$aGFzaA==")]
    public void Verify_Fails_ForAMalformedHash_InsteadOfThrowing(string hash)
    {
        FastHasher().Verify("anything", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_ReadsTheParametersFromTheHash_NotFromConfiguration()
    {
        var hash = FastHasher().Hash("password");

        var differentlyConfigured = new Argon2PasswordHasher(Options.Create(new Argon2Options
        {
            MemorySizeKb = 16_384,
            Iterations = 2,
            DegreeOfParallelism = 1
        }));

        differentlyConfigured.Verify("password", hash).Should().BeTrue();
    }
}