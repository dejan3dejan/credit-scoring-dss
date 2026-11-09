using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Domain;

public class UserAdministrationTests
{
    private static User NewUser(UserRole role = UserRole.Officer) => User.Create("ana", "Ana Jovanović", role, TestData.Now);

    private static User Locked()
    {
        var user = NewUser();
        for (var i = 0; i < User.MaxFailedSignIns; i++)
            user.RecordFailedSignIn(TestData.Now);
        return user;
    }

    [Fact]
    public void A_new_user_is_active_with_a_stamp_and_no_forced_password_change()
    {
        var user = NewUser();

        Assert.True(user.IsActive);
        Assert.False(user.MustChangePassword);
        Assert.NotEqual(Guid.Empty, user.SecurityStamp);
        Assert.Null(user.Email);
    }

    [Fact]
    public void Editing_trims_and_treats_an_empty_email_as_none()
    {
        var user = NewUser();

        user.Edit("  Ana Marić ", "   ", UserRole.Manager, Languages.Serbian);

        Assert.Equal(("Ana Marić", null, UserRole.Manager, "sr"), (user.DisplayName, user.Email, user.Role, user.PreferredLanguage));

        user.Edit("Ana Marić", " ana@bank.rs ", UserRole.Manager, Languages.Serbian);
        Assert.Equal("ana@bank.rs", user.Email);
    }

    [Fact]
    public void Editing_does_not_end_the_session()
    {
        var user = NewUser();
        var stamp = user.SecurityStamp;

        user.Edit("Ana", null, UserRole.Risk, Languages.English);

        Assert.Equal(stamp, user.SecurityStamp);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("")]
    public void An_unknown_language_is_refused(string language)
    {
        var user = NewUser();

        Assert.Throws<ArgumentOutOfRangeException>(() => user.SetPreferredLanguage(language));
        Assert.Throws<ArgumentOutOfRangeException>(() => user.Edit("Ana", null, UserRole.Officer, language));
    }

    [Fact]
    public void An_undefined_role_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewUser().Edit("Ana", null, (UserRole)99, Languages.English));
    }

    [Fact]
    public void Deactivating_ends_the_sessions_and_repeating_it_changes_nothing()
    {
        var user = NewUser();
        var stamp = user.SecurityStamp;

        user.Deactivate();
        var afterFirst = user.SecurityStamp;
        user.Deactivate();

        Assert.False(user.IsActive);
        Assert.NotEqual(stamp, afterFirst);
        Assert.Equal(afterFirst, user.SecurityStamp);
    }

    [Fact]
    public void Reactivating_lifts_the_lockout()
    {
        var user = Locked();
        user.Deactivate();

        user.Reactivate();

        Assert.True(user.IsActive);
        Assert.False(user.IsLockedOut(TestData.Now));
        Assert.Equal(0, user.FailedSignInCount);
    }

    [Fact]
    public void A_reset_password_must_be_changed_and_ends_the_sessions()
    {
        var user = Locked();
        var stamp = user.SecurityStamp;

        user.ResetPassword("temporary-hash");

        Assert.Equal("temporary-hash", user.PasswordHash);
        Assert.True(user.MustChangePassword);
        Assert.False(user.IsLockedOut(TestData.Now));
        Assert.NotEqual(stamp, user.SecurityStamp);
    }

    [Fact]
    public void Changing_the_password_clears_the_forced_change_and_ends_other_sessions()
    {
        var user = NewUser();
        user.ResetPassword("temporary-hash");
        var stamp = user.SecurityStamp;

        user.ChangePassword("own-hash");

        Assert.Equal("own-hash", user.PasswordHash);
        Assert.False(user.MustChangePassword);
        Assert.NotEqual(stamp, user.SecurityStamp);
    }

    [Fact]
    public void Admins_cannot_change_their_own_role()
    {
        var admin = NewUser(UserRole.Admin);

        var refused = Assert.IsType<UserRefused>(
            UserAdministration.Update(admin, admin.Id, "Ana", null, UserRole.Officer, Languages.English));

        Assert.Equal(("role", "own_role"), (refused.Field, refused.Code));
        Assert.Equal(UserRole.Admin, admin.Role);
    }

    [Fact]
    public void Admins_may_edit_the_rest_of_their_own_account()
    {
        var admin = NewUser(UserRole.Admin);

        Assert.IsType<UserChanged>(UserAdministration.Update(admin, admin.Id, "Ana M.", "a@b.rs", UserRole.Admin, Languages.Serbian));

        Assert.Equal(("Ana M.", "sr"), (admin.DisplayName, admin.PreferredLanguage));
    }

    [Fact]
    public void Admins_change_other_users_roles()
    {
        var officer = NewUser();

        Assert.IsType<UserChanged>(UserAdministration.Update(officer, Guid.NewGuid(), "Ana", null, UserRole.Manager, Languages.English));

        Assert.Equal(UserRole.Manager, officer.Role);
    }

    [Fact]
    public void Admins_cannot_deactivate_themselves()
    {
        var admin = NewUser(UserRole.Admin);

        var refused = Assert.IsType<UserRefused>(UserAdministration.Deactivate(admin, admin.Id));

        Assert.Equal((null, "own_account"), (refused.Field, refused.Code));
        Assert.True(admin.IsActive);
    }

    [Fact]
    public void Admins_deactivate_other_users()
    {
        var officer = NewUser();

        Assert.IsType<UserChanged>(UserAdministration.Deactivate(officer, Guid.NewGuid()));

        Assert.False(officer.IsActive);
    }
}
