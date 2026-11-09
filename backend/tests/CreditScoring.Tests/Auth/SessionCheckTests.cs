using System.Security.Claims;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Auth;

public class SessionCheckTests
{
    private static User NewUser() => User.Create("ana", "Ana Jovanović", UserRole.Officer, TestData.Now);

    [Fact]
    public void An_unchanged_active_user_keeps_the_session()
    {
        var user = NewUser();

        Assert.Equal(SessionCheck.Valid, Credentials.Check(Credentials.PrincipalFor(user), user));
    }

    [Fact]
    public void A_user_that_no_longer_exists_loses_the_session()
    {
        Assert.Equal(SessionCheck.Reject, Credentials.Check(Credentials.PrincipalFor(NewUser()), null));
    }

    [Fact]
    public void A_deactivated_user_loses_the_session()
    {
        var user = NewUser();
        var principal = Credentials.PrincipalFor(user);

        user.Deactivate();

        Assert.Equal(SessionCheck.Reject, Credentials.Check(principal, user));
    }

    [Fact]
    public void A_changed_password_ends_the_older_sessions()
    {
        var user = NewUser();
        var principal = Credentials.PrincipalFor(user);

        user.ChangePassword("new-hash");

        Assert.Equal(SessionCheck.Reject, Credentials.Check(principal, user));
    }

    [Fact]
    public void A_cookie_from_before_stamps_existed_is_refused()
    {
        var user = NewUser();
        var old = new ClaimsPrincipal(new ClaimsIdentity(
            Credentials.PrincipalFor(user).Claims.Where(c => c.Type != Credentials.StampClaim), "Cookies"));

        Assert.Equal(SessionCheck.Reject, Credentials.Check(old, user));
    }

    [Fact]
    public void A_changed_role_or_name_refreshes_the_session()
    {
        var user = NewUser();
        var principal = Credentials.PrincipalFor(user);

        user.Edit("Ana Marić", null, UserRole.Officer, Languages.English);
        Assert.Equal(SessionCheck.Refresh, Credentials.Check(principal, user));

        var renamed = Credentials.PrincipalFor(user);
        user.Edit("Ana Marić", null, UserRole.Manager, Languages.English);
        Assert.Equal(SessionCheck.Refresh, Credentials.Check(renamed, user));
    }

    [Fact]
    public void A_refreshed_principal_carries_the_new_role()
    {
        var user = NewUser();
        user.Edit("Ana", null, UserRole.Risk, Languages.English);

        var principal = Credentials.PrincipalFor(user);

        Assert.True(principal.IsInRole(nameof(UserRole.Risk)));
        Assert.Equal(user.SecurityStamp.ToString(), principal.FindFirstValue(Credentials.StampClaim));
    }
}
