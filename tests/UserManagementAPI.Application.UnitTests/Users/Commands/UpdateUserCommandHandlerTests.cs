using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Commands.UpdateUser;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.UnitTests.Users.Commands;

public sealed class UpdateUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private UpdateUserCommandHandler Handler() => new(_userRepository, _unitOfWork);

    [Fact]
    public async Task ChangesEmailAndName_AndSaves()
    {
        var user = TestUsers.Active();
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Handler().HandleAsync(
            new UpdateUserCommand(user.Id, "New.Address@Example.com", "Anastasija", "Petrović"));

        result.IsSuccess.Should().BeTrue();
        user.Email.Value.Should().Be("new.address@example.com");
        user.Name.First.Should().Be("Anastasija");
        _userRepository.Received(1).Update(user);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsNotFound_WhenTheUserDoesNotExist()
    {
        var result = await Handler().HandleAsync(
            new UpdateUserCommand(Guid.NewGuid(), "ana@example.com", "Ana", "Petrović"));

        result.Error.Should().Be(User.NotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsEmailNotUnique_WhenTheNewAddressBelongsToSomeoneElse()
    {
        var user = TestUsers.Active();
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.ExistsByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await Handler().HandleAsync(
            new UpdateUserCommand(user.Id, "taken@example.com", "Ana", "Petrović"));

        result.Error.Should().Be(User.EmailNotUnique);
        user.Email.Value.Should().Be("ana.petrovic@example.com");
    }

    [Fact]
    public async Task DoesNotCheckUniqueness_WhenTheEmailIsUnchanged()
    {
        var user = TestUsers.Active();
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.ExistsByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await Handler().HandleAsync(
            new UpdateUserCommand(user.Id, "ANA.PETROVIC@example.com", "Ana", "Petrović"));

        result.IsSuccess.Should().BeTrue();
        await _userRepository.DidNotReceive().ExistsByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsDeactivated_WhenTheUserIsDeactivated()
    {
        var user = TestUsers.Deactivated();
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Handler().HandleAsync(
            new UpdateUserCommand(user.Id, "new@example.com", "Ana", "Petrović"));

        result.Error.Should().Be(User.Deactivated);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}