using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Commands.LockUser;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.UnitTests.Users.Commands;

public sealed class LockUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private LockUserCommandHandler Handler() => new(_userRepository, _unitOfWork);

    [Fact]
    public async Task LocksAnActiveUser_AndSaves()
    {
        var user = TestUsers.Active();
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Handler().HandleAsync(new LockUserCommand(user.Id));

        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Locked);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsNotFound_WhenTheUserDoesNotExist()
    {
        var result = await Handler().HandleAsync(new LockUserCommand(Guid.NewGuid()));

        result.Error.Should().Be(User.NotFound);
    }

    [Fact]
    public async Task ReturnsAlreadyLocked_AndDoesNotSave_WhenTheUserIsLocked()
    {
        var user = TestUsers.Locked();
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Handler().HandleAsync(new LockUserCommand(user.Id));

        result.Error.Should().Be(User.AlreadyLocked);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}