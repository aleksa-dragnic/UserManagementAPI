using Microsoft.Extensions.DependencyInjection;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Auth;
using UserManagementAPI.Application.Auth.Commands.Login;
using UserManagementAPI.Application.Auth.Commands.Logout;
using UserManagementAPI.Application.Auth.Commands.RefreshToken;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Infrastructure.Persistence.Seed;

namespace UserManagementAPI.IntegrationTests.Auth;

/// <summary>
/// The whole cycle through the dispatcher — behaviors, transaction, real
/// hasher, real token service, real database — as the seeded administrator.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RefreshRotationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LoginRefreshTwiceReplayTheFirst_LeavesEveryTokenForTheUserDead()
    {
        await using var provider = fixture.CreateServiceProvider();
        await SeedAsync(provider);

        var first = (await SendAsync(provider, new LoginCommand(
            DatabaseFixture.AdministratorEmail, DatabaseFixture.AdministratorPassword))).Value;

        var second = (await SendAsync(provider, new RefreshTokenCommand(first.RefreshToken))).Value;
        var third = (await SendAsync(provider, new RefreshTokenCommand(second.RefreshToken))).Value;

        second.RefreshToken.Should().NotBe(first.RefreshToken);
        third.RefreshToken.Should().NotBe(second.RefreshToken);

        // Replay: the first token was already exchanged. The request fails and
        // its transaction rolls back; the revocation of the chain must not.
        var replay = await SendAsync(provider, new RefreshTokenCommand(first.RefreshToken));
        replay.Error.Should().Be(AuthErrors.RefreshTokenReused);

        // The still-fresh third token is now dead too.
        var afterReplay = await SendAsync(provider, new RefreshTokenCommand(third.RefreshToken));
        afterReplay.Error.Should().Be(AuthErrors.InvalidRefreshToken);

        await using var context = fixture.CreateContext();
        context.RefreshTokens.Should().HaveCount(3);
        context.RefreshTokens.Should().OnlyContain(token => token.RevokedAtUtc != null || token.ReplacedById != null);
        context.RefreshTokens.Should().OnlyContain(token => !token.IsActive);
    }

    [Fact]
    public async Task Logout_RevokesTheToken_SoItCannotBeExchanged()
    {
        await using var provider = fixture.CreateServiceProvider();
        await SeedAsync(provider);

        var tokens = (await SendAsync(provider, new LoginCommand(
            DatabaseFixture.AdministratorEmail, DatabaseFixture.AdministratorPassword))).Value;

        (await SendAsync(provider, new LogoutCommand(tokens.RefreshToken))).IsSuccess.Should().BeTrue();
        (await SendAsync(provider, new LogoutCommand(tokens.RefreshToken))).IsSuccess.Should().BeTrue();

        var refresh = await SendAsync(provider, new RefreshTokenCommand(tokens.RefreshToken));
        refresh.Error.Should().Be(AuthErrors.InvalidRefreshToken);
    }

    private static async Task<Result<AuthTokens>> SendAsync(ServiceProvider provider, ICommand<AuthTokens> command)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(command);
    }

    private static async Task<Result> SendAsync(ServiceProvider provider, ICommand command)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(command);
    }

    private static async Task SeedAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        await ActivatorUtilities.CreateInstance<DatabaseSeeder>(scope.ServiceProvider).SeedAsync();
    }
}