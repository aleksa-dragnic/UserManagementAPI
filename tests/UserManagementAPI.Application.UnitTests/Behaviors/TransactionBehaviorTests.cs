using Microsoft.Extensions.DependencyInjection;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Behaviors;
using UserManagementAPI.Application.UnitTests.Dispatching;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.UnitTests.Behaviors;

public sealed class TransactionBehaviorTests
{
    [Fact]
    public async Task RunsTheRestOfThePipelineInsideTheUnitOfWorkTransaction()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var behavior = new TransactionBehavior<TouchCommand, Result>(unitOfWork);

        var response = await behavior.HandleAsync(
            new TouchCommand(),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        unitOfWork.TransactionsOpened.Should().Be(1);
    }

    [Fact]
    public void IsResolvedForCommands_AndSkippedForQueries()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUnitOfWork, RecordingUnitOfWork>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        var provider = services.BuildServiceProvider();

        provider.GetServices<IPipelineBehavior<TouchCommand, Result>>()
            .Should().ContainSingle()
            .Which.Should().BeOfType<TransactionBehavior<TouchCommand, Result>>();

        provider.GetServices<IPipelineBehavior<CountQuery, Result<int>>>()
            .Should().BeEmpty();
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int TransactionsOpened { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
            Func<Task<TResponse>> operation,
            CancellationToken cancellationToken = default)
            where TResponse : Result
        {
            TransactionsOpened++;
            return await operation();
        }
    }
}