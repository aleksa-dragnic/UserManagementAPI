using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Commands.AssignRole;
using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.UnitTests.Users.Commands;

public sealed class AssignRoleCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roleRepository = Substitute.For<IRoleRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private AssignRoleCommandHandler Handler() => new(_userRepository, _roleRepository, _unitOfWork);

    [Fact]
    public async Task AssignsAnExistingRole_AndSaves()
    {
        var user = TestUsers.Active();
        var roleId = Guid.NewGuid();
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _roleRepository.ExistsAsync(roleId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await Handler().HandleAsync(new AssignRoleCommand(user.Id, roleId));

        result.IsSuccess.Should().BeTrue();
        user.Roles.Should().ContainSingle().Which.RoleId.Should().Be(roleId);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsRoleNotFound_WhenTheRoleDoesNotExist()
    {
        var user = TestUsers.Active();
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _roleRepository.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await Handler().HandleAsync(new AssignRoleCommand(user.Id, Guid.NewGuid()));

        result.Error.Should().Be(Role.NotFound);
        user.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task ReturnsUserNotFound_WhenTheUserDoesNotExist()
    {
        var result = await Handler().HandleAsync(new AssignRoleCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.Error.Should().Be(User.NotFound);
        await _roleRepository.DidNotReceive().ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsRoleAlreadyAssigned_WhenTheUserHoldsTheRole()
    {
        var roleId = Guid.NewGuid();
        var user = TestUsers.ActiveWithRoles(roleId);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _roleRepository.ExistsAsync(roleId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await Handler().HandleAsync(new AssignRoleCommand(user.Id, roleId));

        result.Error.Should().Be(User.RoleAlreadyAssigned);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}