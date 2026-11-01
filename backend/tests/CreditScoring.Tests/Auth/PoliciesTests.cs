using CreditScoring.Api.Auth;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CreditScoring.Tests.Auth;

public class PoliciesTests
{
    private static readonly IAuthorizationService Authorization = new ServiceCollection()
        .AddLogging()
        .AddAuthorization(Policies.Configure)
        .BuildServiceProvider()
        .GetRequiredService<IAuthorizationService>();

    private static async Task<bool> Allowed(UserRole role, string policy)
    {
        var principal = Credentials.PrincipalFor(User.Create("u", "U", role, TestData.Now));
        return (await Authorization.AuthorizeAsync(principal, policy)).Succeeded;
    }

    [Theory]
    [InlineData(UserRole.Officer, Policies.CanDecide, true)]
    [InlineData(UserRole.Manager, Policies.CanDecide, true)]
    [InlineData(UserRole.Risk, Policies.CanDecide, false)]
    [InlineData(UserRole.Officer, Policies.CanSeeStats, false)]
    [InlineData(UserRole.Manager, Policies.CanSeeStats, true)]
    [InlineData(UserRole.Risk, Policies.CanSeeStats, true)]
    [InlineData(UserRole.Officer, Policies.CanManageModels, false)]
    [InlineData(UserRole.Manager, Policies.CanManageModels, false)]
    [InlineData(UserRole.Risk, Policies.CanManageModels, true)]
    public async Task Role_policy_matrix(UserRole role, string policy, bool expected)
    {
        Assert.Equal(expected, await Allowed(role, policy));
    }

    [Fact]
    public async Task Anonymous_users_pass_no_policy_and_not_the_fallback()
    {
        var anonymous = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
        var options = new AuthorizationOptions();
        Policies.Configure(options);

        Assert.False((await Authorization.AuthorizeAsync(anonymous, Policies.CanDecide)).Succeeded);
        Assert.False((await Authorization.AuthorizeAsync(anonymous, options.FallbackPolicy!)).Succeeded);
    }
}
