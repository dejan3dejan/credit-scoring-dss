using System.Security.Claims;
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

    public static TheoryData<string, UserRole> Matrix()
    {
        var data = new TheoryData<string, UserRole>();
        foreach (var permission in Permissions.All)
            foreach (var role in Enum.GetValues<UserRole>())
                data.Add(permission, role);
        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Each_permission_admits_exactly_the_roles_that_hold_it(string permission, UserRole role)
    {
        var principal = Credentials.PrincipalFor(User.Create("u", "U", role, TestData.Now));

        var allowed = (await Authorization.AuthorizeAsync(principal, permission)).Succeeded;

        Assert.Equal(Permissions.For(role).Contains(permission), allowed);
    }

    [Fact]
    public async Task Anonymous_users_pass_no_permission_and_not_the_fallback()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var options = new AuthorizationOptions();
        Policies.Configure(options);

        foreach (var permission in Permissions.All)
            Assert.False((await Authorization.AuthorizeAsync(anonymous, permission)).Succeeded);
        Assert.False((await Authorization.AuthorizeAsync(anonymous, options.FallbackPolicy!)).Succeeded);
    }
}
