using System.Security.Claims;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Auth;

public class CredentialsTests
{
    private static User Officer() => User.Create(" Ana.J ", " Ana Jovanović ", UserRole.Officer, TestData.Now);

    [Fact]
    public void User_names_are_normalised_and_get_an_id()
    {
        var user = Officer();

        Assert.Equal("ana.j", user.UserName);
        Assert.Equal("Ana Jovanović", user.DisplayName);
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Fact]
    public void Undefined_role_and_blank_names_are_impossible()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => User.Create("a", "A", (UserRole)9, TestData.Now));
        Assert.ThrowsAny<ArgumentException>(() => User.Create(" ", "A", UserRole.Officer, TestData.Now));
        Assert.ThrowsAny<ArgumentException>(() => User.Create("a", " ", UserRole.Officer, TestData.Now));
    }

    [Fact]
    public void Password_verifies_only_against_its_own_hash()
    {
        var user = Officer();
        user.SetPasswordHash(Credentials.Hash(user, "correct horse"));

        Assert.True(Credentials.Verify(user, "correct horse"));
        Assert.False(Credentials.Verify(user, "wrong"));
        Assert.DoesNotContain("correct horse", user.PasswordHash);
    }

    [Fact]
    public void User_without_a_password_never_verifies()
    {
        Assert.False(Credentials.Verify(Officer(), ""));
    }

    [Fact]
    public void Principal_carries_id_name_and_role_and_reads_back_as_the_actor()
    {
        var user = Officer();

        var principal = Credentials.PrincipalFor(user);

        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.True(principal.IsInRole(nameof(UserRole.Officer)));
        Assert.Equal(new Actor(user.Id, "Ana Jovanović"), Credentials.ActorOf(principal));
    }

    [Fact]
    public void Actor_of_an_anonymous_principal_fails_loud()
    {
        Assert.Throws<InvalidOperationException>(() => Credentials.ActorOf(new ClaimsPrincipal(new ClaimsIdentity())));
    }
}
