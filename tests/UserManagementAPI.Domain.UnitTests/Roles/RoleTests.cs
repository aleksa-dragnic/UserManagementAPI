using System.Reflection;

using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Roles;

namespace UserManagementAPI.Domain.UnitTests.Roles;

public class RoleTests
{
    private static Role Administrator() => Role.Create("Administrator").Value;

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Create_Fails_WhenNameIsMissing(string? name)
    {
        Role.Create(name).Error.Should().Be(Role.NameEmpty);
    }

    [Fact]
    public void Create_Fails_WhenNameExceedsMaxLength()
    {
        Role.Create(new string('a', Role.MaxNameLength + 1)).Error.Should().Be(Role.NameTooLong);
    }

    [Fact]
    public void Create_TrimsTheName_AndStartsWithNoPermissions()
    {
        var result = Role.Create("  Administrator  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Administrator");
        result.Value.Permissions.Should().BeEmpty();
    }

    [Fact]
    public void AddPermission_RecordsTheAssignment()
    {
        var role = Administrator();
        var permissionId = Guid.NewGuid();

        var result = role.AddPermission(permissionId);

        result.IsSuccess.Should().BeTrue();
        role.Permissions.Should().ContainSingle()
            .Which.Should().Match<RolePermission>(rolePermission =>
                rolePermission.RoleId == role.Id && rolePermission.PermissionId == permissionId);
    }

    [Fact]
    public void AddPermission_Fails_WhenTheSamePermissionIsAddedTwice()
    {
        var role = Administrator();
        var permissionId = Guid.NewGuid();
        role.AddPermission(permissionId);

        var result = role.AddPermission(permissionId);

        result.Error.Should().Be(Role.PermissionAlreadyAdded);
        role.Permissions.Should().ContainSingle();
    }

    [Fact]
    public void RemovePermission_DropsTheAssignment()
    {
        var role = Administrator();
        var permissionId = Guid.NewGuid();
        role.AddPermission(permissionId);
        role.AddPermission(Guid.NewGuid());

        var result = role.RemovePermission(permissionId);

        result.IsSuccess.Should().BeTrue();
        role.Permissions.Should().NotContain(rolePermission => rolePermission.PermissionId == permissionId);
    }

    [Fact]
    public void RemovePermission_Fails_WhenTheRoleDoesNotHoldIt()
    {
        Administrator().RemovePermission(Guid.NewGuid()).Error.Should().Be(Role.PermissionNotHeld);
    }

    [Fact]
    public void Permissions_CannotBeMutatedFromOutsideTheAggregate()
    {
        var role = Administrator();

        // Same shape as the seedwork and User tests: assert behaviour, not type.
        var asCollection = (ICollection<RolePermission>)role.Permissions;
        var act = () => asCollection.Clear();

        asCollection.IsReadOnly.Should().BeTrue();
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NoAggregateHoldsANavigationPropertyToAnotherAggregate()
    {
        // Asserted by reflection so the rule fails the build rather than relying
        // on someone remembering it during review.
        var aggregateTypes = typeof(Role).Assembly
            .GetTypes()
            .Where(type => typeof(Entity).IsAssignableFrom(type) && !type.IsAbstract)
            .ToList();

        var offenders = aggregateTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => typeof(AggregateRoot).IsAssignableFrom(property.PropertyType))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .ToList();

        offenders.Should().BeEmpty();
    }
}