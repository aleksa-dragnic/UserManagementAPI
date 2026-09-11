using FluentValidation.TestHelper;

using UserManagementAPI.Application.Users.Commands.RegisterUser;

namespace UserManagementAPI.Application.UnitTests.Users.Commands;

public sealed class RegisterUserCommandValidatorTests
{
    private readonly RegisterUserCommandValidator _validator = new();

    private static readonly RegisterUserCommand Valid =
        new("ana.petrovic@example.com", "Ana", "Petrović", "correct horse battery");

    [Fact]
    public void AcceptsAWellFormedCommand()
    {
        var result = _validator.TestValidate(Valid);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    public void RejectsAMalformedOrMissingEmail(string email)
    {
        var result = _validator.TestValidate(Valid with { Email = email });

        result.ShouldHaveValidationErrorFor(command => command.Email);
    }

    [Fact]
    public void RejectsMissingNames()
    {
        var result = _validator.TestValidate(Valid with { FirstName = "", LastName = " " });

        result.ShouldHaveValidationErrorFor(command => command.FirstName);
        result.ShouldHaveValidationErrorFor(command => command.LastName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short7c")]
    public void RejectsAPasswordOutsideThePolicy(string password)
    {
        var result = _validator.TestValidate(Valid with { Password = password });

        result.ShouldHaveValidationErrorFor(command => command.Password);
    }

    [Fact]
    public void RejectsAPasswordLongerThanTheMaximum()
    {
        var password = new string('x', RegisterUserCommandValidator.MaxPasswordLength + 1);

        var result = _validator.TestValidate(Valid with { Password = password });

        result.ShouldHaveValidationErrorFor(command => command.Password);
    }
}