using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.Domain.Users.Events;

namespace UserManagementAPI.Domain.UnitTests.Users;

public class UserTests
{
    [Fact]
    public void Register_ProducesAPendingUser()
    {
        var result = User.Register(TestUsers.Email(), TestUsers.Name(), TestUsers.Hash(), TestUsers.MemberRoleId, TestClock.Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(UserStatus.Pending);
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.CreatedAtUtc.Should().Be(result.Value.UpdatedAtUtc);
    }

    [Fact]
    public void Register_StartsTheUserWithTheGivenRole()
    {
        var roleId = Guid.NewGuid();

        var user = User.Register(TestUsers.Email(), TestUsers.Name(), TestUsers.Hash(), roleId, TestClock.Now).Value;

        user.Roles.Should().ContainSingle()
            .Which.Should().Match<UserRole>(role => role.UserId == user.Id && role.RoleId == roleId);
    }

    [Fact]
    public void Register_Fails_WithoutARole()
    {
        var result = User.Register(TestUsers.Email(), TestUsers.Name(), TestUsers.Hash(), Guid.Empty, TestClock.Now);

        result.Error.Should().Be(User.InitialRoleRequired);
    }

    [Fact]
    public void Register_RaisesExactlyOneDomainEvent()
    {
        var user = TestUsers.Pending();

        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserRegisteredDomainEvent>()
            .Which.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void DomainEvents_CannotBeMutatedFromOutsideTheAggregate()
    {
        var user = TestUsers.Pending();

        // Same shape as the seedwork test: the wrapper may implement
        // ICollection<T>, what matters is that every mutation path is closed.
        var asCollection = (ICollection<IDomainEvent>)user.DomainEvents;
        var act = () => asCollection.Add(new UserLockedDomainEvent(user.Id, TestClock.Now));

        asCollection.IsReadOnly.Should().BeTrue();
        act.Should().Throw<NotSupportedException>();
        user.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void VerifyEmail_MovesPendingToActive()
    {
        var user = TestUsers.Pending();

        var result = user.VerifyEmail(TestClock.Now);

        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void VerifyEmail_Fails_FromEveryStateOtherThanPending()
    {
        TestUsers.Active().VerifyEmail(TestClock.Now).Error.Should().Be(User.AlreadyVerified);
        TestUsers.Locked().VerifyEmail(TestClock.Now).Error.Should().Be(User.AlreadyVerified);
        TestUsers.Deactivated().VerifyEmail(TestClock.Now).Error.Should().Be(User.AlreadyVerified);
    }

    [Fact]
    public void ChangeEmail_ReplacesTheAddress_AndStampsUpdatedAtWithTheTimeItWasGiven()
    {
        var user = TestUsers.Active();
        var later = TestClock.Now.AddMinutes(5);
        var newEmail = TestUsers.Email("marko.jovanovic@example.com");

        var result = user.ChangeEmail(newEmail, later);

        result.IsSuccess.Should().BeTrue();
        user.Email.Should().Be(newEmail);
        user.UpdatedAtUtc.Should().Be(later);
        user.CreatedAtUtc.Should().Be(TestClock.Now);
    }

    [Fact]
    public void ChangeEmail_ToTheCurrentAddress_ChangesNothing()
    {
        var user = TestUsers.Active();
        var current = user.Email;
        var updatedBefore = user.UpdatedAtUtc;

        var result = user.ChangeEmail(TestUsers.Email(), TestClock.Now.AddMinutes(5));

        result.IsSuccess.Should().BeTrue();
        user.Email.Should().BeSameAs(current);
        user.UpdatedAtUtc.Should().Be(updatedBefore);
    }

    [Fact]
    public void ChangeName_ReplacesTheName_AndStampsUpdatedAtWithTheTimeItWasGiven()
    {
        var user = TestUsers.Active();
        var later = TestClock.Now.AddMinutes(5);
        var newName = TestUsers.Name("Marko", "Jovanović");

        var result = user.ChangeName(newName, later);

        result.IsSuccess.Should().BeTrue();
        user.Name.Should().Be(newName);
        user.UpdatedAtUtc.Should().Be(later);
    }

    [Fact]
    public void ChangeName_ToTheCurrentName_ChangesNothing()
    {
        var user = TestUsers.Active();
        var current = user.Name;
        var updatedBefore = user.UpdatedAtUtc;

        var result = user.ChangeName(TestUsers.Name(), TestClock.Now.AddMinutes(5));

        result.IsSuccess.Should().BeTrue();
        user.Name.Should().BeSameAs(current);
        user.UpdatedAtUtc.Should().Be(updatedBefore);
    }

    [Fact]
    public void ChangeEmail_Fails_WhenTheUserIsDeactivated()
    {
        var user = TestUsers.Deactivated();
        var original = user.Email;

        var result = user.ChangeEmail(TestUsers.Email("marko.jovanovic@example.com"), TestClock.Now);

        result.Error.Should().Be(User.Deactivated);
        user.Email.Should().Be(original);
    }

    [Fact]
    public void ChangeName_Fails_WhenTheUserIsDeactivated()
    {
        var result = TestUsers.Deactivated().ChangeName(TestUsers.Name("Marko", "Jovanović"), TestClock.Now);

        result.Error.Should().Be(User.Deactivated);
    }

    [Fact]
    public void Lock_MovesTheUserToLocked_AndRaisesTheEvent()
    {
        var user = TestUsers.Active();
        user.ClearDomainEvents();

        var result = user.Lock(TestClock.Now);

        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Locked);
        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserLockedDomainEvent>()
            .Which.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void Lock_Fails_WhenTheUserIsAlreadyLocked()
    {
        var user = TestUsers.Locked();
        user.ClearDomainEvents();

        var result = user.Lock(TestClock.Now);

        result.Error.Should().Be(User.AlreadyLocked);
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Lock_Fails_WhenTheUserIsDeactivated()
    {
        TestUsers.Deactivated().Lock(TestClock.Now).Error.Should().Be(User.Deactivated);
    }

    [Fact]
    public void Unlock_ReturnsALockedUserToActive()
    {
        var user = TestUsers.Locked();

        var result = user.Unlock(TestClock.Now);

        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Unlock_Fails_WhenTheUserIsNotLocked()
    {
        TestUsers.Pending().Unlock(TestClock.Now).Error.Should().Be(User.NotLocked);
        TestUsers.Active().Unlock(TestClock.Now).Error.Should().Be(User.NotLocked);
        TestUsers.Deactivated().Unlock(TestClock.Now).Error.Should().Be(User.NotLocked);
    }

    [Fact]
    public void Deactivate_MovesTheUserToDeactivated_FromAnyActiveState()
    {
        var pending = TestUsers.Pending();
        var locked = TestUsers.Locked();

        pending.Deactivate(TestClock.Now).IsSuccess.Should().BeTrue();
        locked.Deactivate(TestClock.Now).IsSuccess.Should().BeTrue();

        pending.Status.Should().Be(UserStatus.Deactivated);
        locked.Status.Should().Be(UserStatus.Deactivated);
    }

    [Fact]
    public void Deactivate_Fails_WhenTheUserIsAlreadyDeactivated()
    {
        TestUsers.Deactivated().Deactivate(TestClock.Now).Error.Should().Be(User.AlreadyDeactivated);
    }
    [Fact]
    public void AssignRole_RecordsTheAssignment()
    {
        var user = TestUsers.Active();
        var roleId = Guid.NewGuid();

        var result = user.AssignRole(roleId, TestClock.Now);

        result.IsSuccess.Should().BeTrue();
        user.Roles.Should().HaveCount(2).And
            .Contain(role => role.UserId == user.Id && role.RoleId == roleId);
    }

    [Fact]
    public void AssignRole_Fails_WhenTheSameRoleIsAssignedTwice()
    {
        var roleId = Guid.NewGuid();
        var user = TestUsers.Active(roleId);

        var result = user.AssignRole(roleId, TestClock.Now);

        result.Error.Should().Be(User.RoleAlreadyAssigned);
        user.Roles.Should().ContainSingle();
    }

    [Fact]
    public void AssignRole_Fails_WhenTheUserIsDeactivated()
    {
        TestUsers.Deactivated().AssignRole(Guid.NewGuid(), TestClock.Now).Error.Should().Be(User.Deactivated);
    }

    [Fact]
    public void RemoveRole_DropsTheAssignment_WhenAnotherRoleRemains()
    {
        var first = Guid.NewGuid();
        var user = TestUsers.Active(first);
        user.AssignRole(Guid.NewGuid(), TestClock.Now);

        var result = user.RemoveRole(first, TestClock.Now);

        result.IsSuccess.Should().BeTrue();
        user.Roles.Should().ContainSingle().Which.RoleId.Should().NotBe(first);
    }

    [Fact]
    public void RemoveRole_Fails_WhenItIsTheLastRemainingRole()
    {
        var roleId = Guid.NewGuid();
        var user = TestUsers.Active(roleId);

        var result = user.RemoveRole(roleId, TestClock.Now);

        result.Error.Should().Be(User.LastRoleCannotBeRemoved);
        user.Roles.Should().ContainSingle();
    }

    [Fact]
    public void RemoveRole_Fails_WhenTheUserDoesNotHoldTheRole()
    {
        var user = TestUsers.Active();
        user.AssignRole(Guid.NewGuid(), TestClock.Now);

        var result = user.RemoveRole(Guid.NewGuid(), TestClock.Now);

        result.Error.Should().Be(User.RoleNotAssigned);
    }
}
