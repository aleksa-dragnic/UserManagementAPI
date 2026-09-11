using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Commands.RegisterUser;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.UnitTests.Users.Commands;

public sealed class RegisterUserCommandHandlerTests
{
    private static readonly RegisterUserCommand ValidCommand =
        new("Ana.Petrovic@Example.com", "Ana", "Petrović", "correct horse battery");

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private RegisterUserCommandHandler Handler() => new(_userRepository, _passwordHasher, _unitOfWork);

    [Fact]
    public async Task RegistersAPendingUser_AndReturnsItsId()
    {
        _passwordHasher.Hash(ValidCommand.Password).Returns("pbkdf2-sha256$1$c2FsdA==$aGFzaA==");

        var result = await Handler().HandleAsync(ValidCommand);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        _userRepository.Received(1).Add(Arg.Is<User>(user =>
            user.Id == result.Value &&
            user.Email.Value == "ana.petrovic@example.com" &&
            user.Status == UserStatus.Pending &&
            user.PasswordHash.Value == "pbkdf2-sha256$1$c2FsdA==$aGFzaA=="));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsEmailNotUnique_AndDoesNotAdd_WhenTheEmailIsTaken()
    {
        _userRepository.ExistsByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await Handler().HandleAsync(ValidCommand);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(User.EmailNotUnique);

        _userRepository.DidNotReceive().Add(Arg.Any<User>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsTheValueObjectError_WhenTheEmailIsMalformed()
    {
        var command = ValidCommand with { Email = "not-an-email" };

        var result = await Handler().HandleAsync(command);

        result.Error.Should().Be(Email.InvalidFormat);
        _userRepository.DidNotReceive().Add(Arg.Any<User>());
    }

    [Fact]
    public async Task NeverPassesThePlaintextPasswordToTheAggregate()
    {
        _passwordHasher.Hash(ValidCommand.Password).Returns("hashed-value");

        await Handler().HandleAsync(ValidCommand);

        _userRepository.Received(1).Add(Arg.Is<User>(user =>
            user.PasswordHash.Value == "hashed-value" &&
            user.PasswordHash.Value != ValidCommand.Password));
    }
}