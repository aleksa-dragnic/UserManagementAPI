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
            PasswordHash.Create("$argon2id$v=19$m=1,t=1,p=1$c2FsdA==$aGFzaA==").Value,
            Guid.NewGuid(),
            DateTime.UtcNow).Value;
        // The fake clock starts at the real now: the handler below validates the
        // token's lifetime against the real clock, and the test pins only the
        // instant the service issues it at.
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = new JwtTokenService(Options.Create(TestOptions), clock);

        var issued = service.CreateAccessToken(user, ["users.read", "users.write"]);

        issued.ExpiresAtUtc.Should().Be(clock.GetUtcNow().UtcDateTime.AddMinutes(15));

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
        var act = () => new JwtTokenService(
            Options.Create(new JwtOptions
            {
                SigningKey = "too-short"
            }),
            TimeProvider.System);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Jwt:SigningKey*");
    }

    [Fact]
    public void ARefreshToken_ExpiresExactlyOneLifetimeAfterTheClock()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
        var service = new JwtTokenService(
            Options.Create(new JwtOptions
            {
                SigningKey = TestOptions.SigningKey,
                RefreshTokenLifetime = TimeSpan.FromDays(7)
            }),
            clock);

        service.CreateRefreshToken().ExpiresAtUtc.Should().Be(new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc));

        clock.Advance(TimeSpan.FromHours(1));
        service.CreateRefreshToken().ExpiresAtUtc.Should().Be(new DateTime(2026, 12, 1, 13, 0, 0, DateTimeKind.Utc));
    }
}