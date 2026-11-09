using CreditScoring.Api.Auth;
using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Auth;

public class PermissionsTests
{
    [Fact]
    public void Admin_permissions()
    {
        Assert.Equal(["audit.read", "branches.manage", "users.manage"], Permissions.For(UserRole.Admin));
    }

    [Fact]
    public void Officer_permissions()
    {
        Assert.Equal(
            ["applications.decide", "applications.read", "applications.write", "clients.read", "clients.write", "models.read"],
            Permissions.For(UserRole.Officer));
    }

    [Fact]
    public void Manager_permissions()
    {
        Assert.Equal(
            [
                "applications.approve-escalated", "applications.assign", "applications.decide", "applications.read",
                "applications.write", "clients.read", "clients.write", "models.read", "stats.read",
            ],
            Permissions.For(UserRole.Manager));
    }

    [Fact]
    public void Risk_permissions()
    {
        Assert.Equal(
            ["applications.read", "audit.read", "clients.read", "models.manage", "models.read", "outcomes.record", "stats.read"],
            Permissions.For(UserRole.Risk));
    }

    [Fact]
    public void Undefined_role_has_no_permission_list()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Permissions.For((UserRole)99));
    }

    [Fact]
    public void Every_permission_is_listed_once_and_held_by_some_role()
    {
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct().Count());
        Assert.All(Permissions.All, p => Assert.NotEmpty(Permissions.RolesWith(p)));
        Assert.Equal(
            Permissions.All.Order(),
            Enum.GetValues<UserRole>().SelectMany(Permissions.For).Distinct().Order());
    }

    [Theory]
    [InlineData("users.manage", new[] { UserRole.Admin })]
    [InlineData("audit.read", new[] { UserRole.Admin, UserRole.Risk })]
    [InlineData("applications.decide", new[] { UserRole.Officer, UserRole.Manager })]
    [InlineData("models.read", new[] { UserRole.Officer, UserRole.Manager, UserRole.Risk })]
    public void Roles_with_a_permission(string permission, UserRole[] roles)
    {
        Assert.Equal(roles.Order(), Permissions.RolesWith(permission).Order());
    }

    [Fact]
    public void An_unknown_permission_fails_loudly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Permissions.RolesWith("clients.delete"));
    }
}
