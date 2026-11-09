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

        user.Edit("Ana Marić", null, UserRole.Officer, Languages.English, null);
        Assert.Equal(SessionCheck.Refresh, Credentials.Check(principal, user));

        var renamed = Credentials.PrincipalFor(user);
        user.Edit("Ana Marić", null, UserRole.Manager, Languages.English, null);
        Assert.Equal(SessionCheck.Refresh, Credentials.Check(renamed, user));
    }

    [Fact]
    public void A_refreshed_principal_carries_the_new_role()
    {
        var user = NewUser();
        user.Edit("Ana", null, UserRole.Risk, Languages.English, null);

        var principal = Credentials.PrincipalFor(user);

        Assert.True(principal.IsInRole(nameof(UserRole.Risk)));
        Assert.Equal(user.SecurityStamp.ToString(), principal.FindFirstValue(Credentials.StampClaim));
    }

    [Fact]
    public void A_move_to_another_branch_refreshes_the_session()
    {
        var user = User.Create("ana", "Ana", UserRole.Officer, TestData.Now, TestData.BranchId);
        var principal = Credentials.PrincipalFor(user);

        user.Edit("Ana", null, UserRole.Officer, Languages.English, Guid.NewGuid());

        Assert.Equal(SessionCheck.Refresh, Credentials.Check(principal, user));
    }

    [Fact]
    public void Officers_and_managers_are_scoped_to_their_branch_and_others_are_not()
    {
        var officer = User.Create("o", "O", UserRole.Officer, TestData.Now, TestData.BranchId);
        var manager = User.Create("m", "M", UserRole.Manager, TestData.Now, TestData.BranchId);

        Assert.Equal(TestData.BranchId, Credentials.BranchScopeOf(Credentials.PrincipalFor(officer)));
        Assert.Equal(TestData.BranchId, Credentials.BranchScopeOf(Credentials.PrincipalFor(manager)));
        Assert.Null(Credentials.BranchScopeOf(Credentials.PrincipalFor(User.Create("r", "R", UserRole.Risk, TestData.Now))));
        Assert.Null(Credentials.BranchScopeOf(Credentials.PrincipalFor(User.Create("a", "A", UserRole.Admin, TestData.Now))));
    }

    [Fact]
    public void An_officer_without_a_branch_fails_loudly_instead_of_seeing_everything()
    {
        var officer = Credentials.PrincipalFor(User.Create("o", "O", UserRole.Officer, TestData.Now));

        Assert.Throws<InvalidOperationException>(() => Credentials.BranchScopeOf(officer));
    }
}
