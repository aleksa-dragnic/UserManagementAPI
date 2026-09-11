using FluentValidation;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Behaviors;
using UserManagementAPI.Application.Common;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.UnitTests.Behaviors;

public sealed class ValidationBehaviorTests
{
    private sealed record NameCommand(string Name) : ICommand;

    private sealed record NameQuery(string Name) : IQuery<int>;

    [Fact]
    public async Task ReturnsAFailure_AndNeverCallsTheHandler_WhenAValidatorFails()
    {
        var validator = new InlineValidator<NameCommand>();
        validator.RuleFor(command => command.Name).NotEmpty();
        var behavior = new ValidationBehavior<NameCommand, Result>([validator]);
        var handlerCalled = false;

        var response = await behavior.HandleAsync(
            new NameCommand(string.Empty),
            () =>
            {
                handlerCalled = true;
                return Task.FromResult(Result.Success());
            },
            CancellationToken.None);

        handlerCalled.Should().BeFalse();
        response.IsFailure.Should().BeTrue();
        response.Error.Should().BeOfType<ValidationError>()
            .Which.Errors.Should().ContainSingle()
            .Which.Code.Should().Be("Validation.Name");
    }

    [Fact]
    public async Task BuildsTheFailureAsTheHandlersResponseType_ForAResultWithAValue()
    {
        var validator = new InlineValidator<NameQuery>();
        validator.RuleFor(query => query.Name).NotEmpty();
        var behavior = new ValidationBehavior<NameQuery, Result<int>>([validator]);

        var response = await behavior.HandleAsync(
            new NameQuery(string.Empty),
            () => Task.FromResult(Result.Success(1)),
            CancellationToken.None);

        response.Should().BeOfType<Result<int>>();
        response.IsFailure.Should().BeTrue();
        response.Error.Code.Should().Be(ValidationError.GeneralCode);
    }

    [Fact]
    public async Task CallsTheHandler_WhenThereAreNoValidators()
    {
        var behavior = new ValidationBehavior<NameCommand, Result>([]);

        var response = await behavior.HandleAsync(
            new NameCommand("anything"),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CallsTheHandler_WhenEveryValidatorPasses()
    {
        var validator = new InlineValidator<NameCommand>();
        validator.RuleFor(command => command.Name).NotEmpty();
        var behavior = new ValidationBehavior<NameCommand, Result>([validator]);

        var response = await behavior.HandleAsync(
            new NameCommand("Ana"),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
    }
}