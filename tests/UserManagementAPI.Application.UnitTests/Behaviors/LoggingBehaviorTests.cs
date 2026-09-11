using Microsoft.Extensions.Logging;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Behaviors;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.UnitTests.Behaviors;

public sealed class LoggingBehaviorTests
{
    private sealed record SensitiveCommand(string Password) : ICommand;

    [Fact]
    public async Task DoesNotWriteTheRequestPayload()
    {
        var logger = new RecordingLogger<LoggingBehavior<SensitiveCommand, Result>>();
        var behavior = new LoggingBehavior<SensitiveCommand, Result>(logger);

        await behavior.HandleAsync(
            new SensitiveCommand("hunter2-do-not-log"),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        logger.Entries.Should().NotBeEmpty();
        logger.Entries.Should().AllSatisfy(entry => entry.Should().NotContain("hunter2"));
        logger.Entries.Should().Contain(entry => entry.Contains(nameof(SensitiveCommand)));
    }

    [Fact]
    public async Task LogsTheErrorCode_WhenTheHandlerFails()
    {
        var logger = new RecordingLogger<LoggingBehavior<SensitiveCommand, Result>>();
        var behavior = new LoggingBehavior<SensitiveCommand, Result>(logger);
        var error = new Error("Test.Failed", "It failed.");

        var response = await behavior.HandleAsync(
            new SensitiveCommand("irrelevant"),
            () => Task.FromResult(Result.Failure(error)),
            CancellationToken.None);

        response.Error.Should().Be(error);
        logger.Entries.Should().Contain(entry => entry.Contains("Test.Failed"));
    }

    /// <summary>
    /// Captures both the formatted message and every structured value, so a
    /// payload hidden in a property placeholder would be caught as well.
    /// </summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(formatter(state, exception));

            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                Entries.AddRange(values.Select(pair => pair.Value?.ToString() ?? string.Empty));
            }
        }
    }
}