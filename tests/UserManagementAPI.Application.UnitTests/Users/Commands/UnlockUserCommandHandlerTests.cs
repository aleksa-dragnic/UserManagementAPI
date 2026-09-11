using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Commands.UnlockUser;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.UnitTests.Users.Commands;

public sealed class UnlockUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private UnlockUserCommandHandler Handler() => new(_userRepository, _unitOfWork);

    [Fact]
    public async Task UnlocksALockedUser_AndSaves()
    {
        var user = TestUsers.Locked();
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Handler().HandleAsync(new UnlockUserCommand(user.Id));

        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsNotLocked_WhenTheUserIsNotLocked()
    {
        var user = TestUsers.Active();
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Handler().HandleAsync(new UnlockUserCommand(user.Id));

        result.Error.Should().Be(User.NotLocked);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsNotFound_WhenTheUserDoesNotExist()
    {
        var result = await Handler().HandleAsync(new UnlockUserCommand(Guid.NewGuid()));

        result.Error.Should().Be(User.NotFound);
    }
}