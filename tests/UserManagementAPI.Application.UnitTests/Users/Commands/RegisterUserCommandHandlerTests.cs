using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Commands.RegisterUser;
using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.UnitTests.Users.Commands;

public sealed class RegisterUserCommandHandlerTests
{
    private static readonly RegisterUserCommand ValidCommand =
        new("Ana.Petrovic@Example.com", "Ana", "Petrović", "correct horse battery");

    private static readonly Role Member = Role.Create(RoleNames.Member).Value;

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roleRepository = Substitute.For<IRoleRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public RegisterUserCommandHandlerTests()
    {
        _roleRepository.GetByNameAsync(RoleNames.Member, Arg.Any<CancellationToken>()).Returns(Member);
    }

    private RegisterUserCommandHandler Handler() =>
        new(_userRepository, _roleRepository, _passwordHasher, _unitOfWork);

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
    public async Task StartsTheUserWithTheMemberRole()
    {
        _passwordHasher.Hash(ValidCommand.Password).Returns("pbkdf2-sha256$1$c2FsdA==$aGFzaA==");

        await Handler().HandleAsync(ValidCommand);

        _userRepository.Received(1).Add(Arg.Is<User>(user =>
            user.Roles.Count == 1 && user.Roles.Single().RoleId == Member.Id));
    }

    [Fact]
    public async Task Throws_AndDoesNotAdd_WhenTheMemberRoleIsMissing()
    {
        _passwordHasher.Hash(ValidCommand.Password).Returns("pbkdf2-sha256$1$c2FsdA==$aGFzaA==");
        _roleRepository.GetByNameAsync(RoleNames.Member, Arg.Any<CancellationToken>()).Returns((Role?)null);

        var act = () => Handler().HandleAsync(ValidCommand);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _userRepository.DidNotReceive().Add(Arg.Any<User>());
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