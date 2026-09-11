using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.Infrastructure.Identity;

namespace UserManagementAPI.IntegrationTests.Identity;

public sealed class JwtTokenServiceTests
{
    private static readonly JwtOptions TestOptions = new()
    {
        SigningKey = new string('k', 64),
        Issuer = "test-issuer",
        Audience = "test-audience",
        AccessTokenLifetime = TimeSpan.FromMinutes(15)
    };

    [Fact]
    public async Task CreatesAValidToken_WithSubjectEmailAndPermissionClaims_ThatExpiresInFifteenMinutes()
    {
        var user = User.Register(
            Email.Create("ana.petrovic@example.com").Value,
            PersonName.Create("Ana", "Petrović").Value,
            PasswordHash.Create("$argon2id$v=19$m=1,t=1,p=1$c2FsdA==$aGFzaA==").Value).Value;
        var service = new JwtTokenService(Options.Create(TestOptions));

        var issued = service.CreateAccessToken(user, ["users.read", "users.write"]);

        issued.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(issued.Value, new TokenValidationParameters
        {
            ValidIssuer = TestOptions.Issuer,
            ValidAudience = TestOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestOptions.SigningKey)),
            ClockSkew = TimeSpan.Zero
        });

        validation.IsValid.Should().BeTrue();
        validation.ClaimsIdentity.FindFirst(JwtRegisteredClaimNames.Sub)!.Value.Should().Be(user.Id.ToString());
        validation.ClaimsIdentity.FindFirst(JwtRegisteredClaimNames.Email)!.Value.Should().Be("ana.petrovic@example.com");
        validation.ClaimsIdentity.FindAll(AuthClaimTypes.Permission).Select(claim => claim.Value)
            .Should().BeEquivalentTo("users.read", "users.write");
        validation.ClaimsIdentity.FindFirst("role").Should().BeNull();
    }

    [Fact]
    public void RefusesToStart_WithAShortSigningKey()
    {
        var act = () => new JwtTokenService(Options.Create(new JwtOptions
        {
            SigningKey = "too-short"
        }));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Jwt:SigningKey*");
    }
}