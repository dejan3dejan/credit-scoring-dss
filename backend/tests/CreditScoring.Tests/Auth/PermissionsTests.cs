using CreditScoring.Api.Auth;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CreditScoring.Tests.Auth;

public class PermissionsTests
{
    [Fact]
    public void Officer_permissions()
    {
        Assert.Equal(
            ["applications.decide", "applications.read", "applications.write", "clients.read", "clients.write"],
            Permissions.For(UserRole.Officer));
    }

    [Fact]
    public void Manager_permissions()
    {
        Assert.Equal(
            [
                "applications.approve-escalated", "applications.assign", "applications.decide", "applications.read",
                "applications.write", "clients.read", "clients.write", "stats.read",
            ],
            Permissions.For(UserRole.Manager));
    }

    [Fact]
    public void Risk_permissions()
    {
        Assert.Equal(
            ["applications.read", "audit.read", "clients.read", "models.manage", "outcomes.record", "stats.read"],
            Permissions.For(UserRole.Risk));
    }

    [Fact]
    public void Undefined_role_has_no_permission_list()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Permissions.For((UserRole)99));
    }

    [Theory]
    [InlineData(Policies.CanDecide, Permissions.ApplicationsDecide)]
    [InlineData(Policies.CanSeeStats, Permissions.StatsRead)]
    [InlineData(Policies.CanManageModels, Permissions.ModelsManage)]
    public async Task Todays_role_policies_agree_with_the_permission_map(string policy, string permission)
    {
        var authorization = new ServiceCollection()
            .AddLogging()
            .AddAuthorization(Policies.Configure)
            .BuildServiceProvider()
            .GetRequiredService<IAuthorizationService>();

        foreach (var role in Enum.GetValues<UserRole>())
        {
            var principal = Credentials.PrincipalFor(User.Create("u", "U", role, TestData.Now));
            var allowed = (await authorization.AuthorizeAsync(principal, policy)).Succeeded;
            Assert.Equal(Permissions.For(role).Contains(permission), allowed);
        }
    }
}
