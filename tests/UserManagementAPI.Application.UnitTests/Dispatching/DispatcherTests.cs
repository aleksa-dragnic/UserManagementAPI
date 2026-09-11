using Microsoft.Extensions.DependencyInjection;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Dispatching;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.UnitTests.Dispatching;

public sealed class DispatcherTests
{
    [Fact]
    public async Task SendAsync_ResolvesTheHandlerForACommandWithAResponse()
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<PingCommand, string>, PingCommandHandler>();
        var dispatcher = new Dispatcher(services.BuildServiceProvider());

        var result = await dispatcher.SendAsync(new PingCommand("hi"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("pong: hi");
    }

    [Fact]
    public async Task SendAsync_ResolvesTheHandlerForACommandWithoutAResponse()
    {
        var handler = new TouchCommandHandler();
        var services = new ServiceCollection();
        services.AddSingleton<ICommandHandler<TouchCommand>>(handler);
        var dispatcher = new Dispatcher(services.BuildServiceProvider());

        var result = await dispatcher.SendAsync(new TouchCommand());

        result.IsSuccess.Should().BeTrue();
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task QueryAsync_ResolvesTheQueryHandler()
    {
        var services = new ServiceCollection();
        services.AddScoped<IQueryHandler<CountQuery, int>, CountQueryHandler>();
        var dispatcher = new Dispatcher(services.BuildServiceProvider());

        var result = await dispatcher.QueryAsync(new CountQuery());

        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task SendAsync_ThrowsAClearException_WhenNoHandlerIsRegistered()
    {
        var dispatcher = new Dispatcher(new ServiceCollection().BuildServiceProvider());

        var act = async () => await dispatcher.SendAsync(new OrphanCommand());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{nameof(OrphanCommand)}*");
    }

    [Fact]
    public async Task Behaviors_ExecuteInRegistrationOrder_AndWrapTheHandler()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<PingCommand, string>, PingCommandHandler>();
        services.AddSingleton<IPipelineBehavior<PingCommand, Result<string>>>(
            new RecordingBehavior<PingCommand, Result<string>>("outer", log));
        services.AddSingleton<IPipelineBehavior<PingCommand, Result<string>>>(
            new RecordingBehavior<PingCommand, Result<string>>("inner", log));
        var dispatcher = new Dispatcher(services.BuildServiceProvider());

        var result = await dispatcher.SendAsync(new PingCommand("ordered"));

        result.IsSuccess.Should().BeTrue();
        log.Should().Equal("outer:before", "inner:before", "inner:after", "outer:after");
    }

    [Fact]
    public void AddApplication_RegistersTheDispatcher()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IDispatcher) &&
            descriptor.ImplementationType == typeof(Dispatcher));
    }
}