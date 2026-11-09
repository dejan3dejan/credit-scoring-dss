using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Domain;

public class UserSignInTests
{
    private static User NewUser() => User.Create("ana", "Ana", UserRole.Officer, TestData.Now);

    private static void Fail(User user, int times, DateTimeOffset at)
    {
        for (var i = 0; i < times; i++)
            user.RecordFailedSignIn(at);
    }

    [Fact]
    public void New_user_speaks_english_and_has_a_clean_sign_in_record()
    {
        var user = NewUser();

        Assert.Equal(Languages.English, user.PreferredLanguage);
        Assert.Equal(0, user.FailedSignInCount);
        Assert.Null(user.LockedUntil);
        Assert.Null(user.LastSignedInAt);
        Assert.False(user.IsLockedOut(TestData.Now));
    }

    [Fact]
    public void Four_wrong_passwords_count_but_do_not_lock()
    {
        var user = NewUser();

        Fail(user, User.MaxFailedSignIns - 1, TestData.Now);

        Assert.Equal(4, user.FailedSignInCount);
        Assert.False(user.IsLockedOut(TestData.Now));
    }

    [Fact]
    public void Fifth_wrong_password_locks_for_fifteen_minutes()
    {
        var user = NewUser();

        Fail(user, User.MaxFailedSignIns, TestData.Now);

        Assert.Equal(TestData.Now.AddMinutes(15), user.LockedUntil);
        Assert.True(user.IsLockedOut(TestData.Now.AddMinutes(15).AddTicks(-1)));
        Assert.False(user.IsLockedOut(TestData.Now.AddMinutes(15)));
        Assert.Equal(0, user.FailedSignInCount);
    }

    [Fact]
    public void A_locked_account_records_neither_failures_nor_sign_ins()
    {
        var user = NewUser();
        Fail(user, User.MaxFailedSignIns, TestData.Now);

        Assert.Throws<InvalidOperationException>(() => user.RecordFailedSignIn(TestData.Now.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => user.RecordSignIn(TestData.Now.AddMinutes(1)));
    }

    [Fact]
    public void Sign_in_clears_the_failures_and_records_the_time()
    {
        var user = NewUser();
        Fail(user, 3, TestData.Now);

        user.RecordSignIn(TestData.Now.AddMinutes(1));

        Assert.Equal(0, user.FailedSignInCount);
        Assert.Null(user.LockedUntil);
        Assert.Equal(TestData.Now.AddMinutes(1), user.LastSignedInAt);
    }

    [Fact]
    public void After_the_lock_expires_failures_count_from_zero_again()
    {
        var user = NewUser();
        Fail(user, User.MaxFailedSignIns, TestData.Now);
        var later = TestData.Now.AddMinutes(16);

        Fail(user, 1, later);

        Assert.Equal(1, user.FailedSignInCount);
        Assert.False(user.IsLockedOut(later));
    }

    [Theory]
    [InlineData(" Ana.J ", "ana.j")]
    [InlineData("OFFICER", "officer")]
    public void User_names_normalise_the_same_way_everywhere(string typed, string stored)
    {
        Assert.Equal(stored, User.NormalizeUserName(typed));
        Assert.Equal(stored, User.Create(typed, "A", UserRole.Officer, TestData.Now).UserName);
    }
}
