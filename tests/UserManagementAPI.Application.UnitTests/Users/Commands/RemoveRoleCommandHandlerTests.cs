using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Commands.RemoveRole;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.UnitTests.Users.Commands;

public sealed class RemoveRoleCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private RemoveRoleCommandHandler Handler() => new(_userRepository, _unitOfWork);

    [Fact]
    public async Task RemovesARole_WhenAnotherRemains()
    {
        var keep = Guid.NewGuid();
        var remove = Guid.NewGuid();
        var user = TestUsers.ActiveWithRoles(keep, remove);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Handler().HandleAsync(new RemoveRoleCommand(user.Id, remove));

        result.IsSuccess.Should().BeTrue();
        user.Roles.Should().ContainSingle().Which.RoleId.Should().Be(keep);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsLastRoleCannotBeRemoved_WhenItIsTheOnlyRole()
    {
        var only = Guid.NewGuid();
        var user = TestUsers.ActiveWithRoles(only);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Handler().HandleAsync(new RemoveRoleCommand(user.Id, only));

        result.Error.Should().Be(User.LastRoleCannotBeRemoved);
        user.Roles.Should().ContainSingle();
    }

    [Fact]
    public async Task ReturnsRoleNotAssigned_WhenTheUserDoesNotHoldTheRole()
    {
        var user = TestUsers.ActiveWithRoles(Guid.NewGuid(), Guid.NewGuid());
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Handler().HandleAsync(new RemoveRoleCommand(user.Id, Guid.NewGuid()));

        result.Error.Should().Be(User.RoleNotAssigned);
    }

    [Fact]
    public async Task ReturnsNotFound_WhenTheUserDoesNotExist()
    {
        var result = await Handler().HandleAsync(new RemoveRoleCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.Error.Should().Be(User.NotFound);
    }
}