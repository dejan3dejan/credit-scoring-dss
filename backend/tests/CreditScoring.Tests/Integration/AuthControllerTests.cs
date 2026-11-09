using System.Security.Claims;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Controllers;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class AuthControllerTests(DatabaseFixture database) : IAsyncLifetime
{
    private const string Password = "correct horse battery";

    private readonly ManualTimeProvider clock = new(TestData.Now);
    private readonly RecordingAuthentication authentication = new();

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class RecordingAuthentication : IAuthenticationService
    {
        public ClaimsPrincipal? SignedIn { get; private set; }
        public int SignOuts { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, scheme);
            SignedIn = principal;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, scheme);
            SignOuts++;
            return Task.CompletedTask;
        }
    }

    private async Task<User> NewUser(UserRole role = UserRole.Officer)
    {
        var user = User.Create($"user-{Guid.NewGuid():N}"[..20], "Ana Jovanović", role, TestData.Now);
        user.SetPasswordHash(Credentials.Hash(user, Password));
        await using var db = database.CreateContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<T> WithAuth<T>(Func<AuthController, Task<T>> action, ClaimsPrincipal? user = null)
    {
        await using var db = database.CreateContext();
        var services = TestServices.Api()
            .AddSingleton<IAuthenticationService>(authentication)
            .BuildServiceProvider();
        var controller = new AuthController(new SignInService(db, clock), db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services, User = user ?? new ClaimsPrincipal(new ClaimsIdentity()) },
            },
        };
        return await action(controller);
    }

    private Task<ActionResult<MeResponse>> Login(string userName, string password) =>
        WithAuth(c => c.Login(new LoginRequest { UserName = userName, Password = password }, CancellationToken.None));

    private static ProblemDetails Problem<T>(ActionResult<T> result, int status, string code)
    {
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(status, objectResult.StatusCode);
        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Equal(code, problem.Extensions["code"]);
        return problem;
    }

    private async Task<User> Reload(User user)
    {
        await using var db = database.CreateContext();
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
    }

    [Fact]
    public async Task Login_signs_in_and_returns_the_user()
    {
        var user = await NewUser();

        var response = await Login($"  {user.UserName.ToUpperInvariant()} ", Password);

        var me = response.Value!;
        Assert.Equal(user.Id, me.Id);
        Assert.Equal(user.UserName, me.UserName);
        Assert.Equal("Ana Jovanović", me.DisplayName);
        Assert.Equal(UserRole.Officer, me.Role);
        Assert.Null(me.Branch);
        Assert.Equal(Permissions.For(UserRole.Officer), me.Permissions);
        Assert.Equal("en", me.PreferredLanguage);
        Assert.Equal(new Actor(user.Id, "Ana Jovanović"), Credentials.ActorOf(authentication.SignedIn!));
        Assert.Equal(TestData.Now, (await Reload(user)).LastSignedInAt);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_user_get_the_same_answer()
    {
        var user = await NewUser();

        var wrong = await Login(user.UserName, "wrong");
        var unknown = await Login("nobody-here", Password);

        Assert.Equal(
            Problem(wrong, 401, "invalid_credentials").Title,
            Problem(unknown, 401, "invalid_credentials").Title);
        Assert.Null(authentication.SignedIn);
        Assert.Equal(1, (await Reload(user)).FailedSignInCount);
    }

    [Fact]
    public async Task Fifth_wrong_password_locks_the_account_and_says_until_when()
    {
        var user = await NewUser();
        for (var i = 0; i < User.MaxFailedSignIns - 1; i++)
            Problem(await Login(user.UserName, "wrong"), 401, "invalid_credentials");

        var fifth = await Login(user.UserName, "wrong");

        var problem = Problem(fifth, 401, "locked_out");
        Assert.Equal(TestData.Now.AddMinutes(15), problem.Extensions["lockedUntil"]);
    }

    [Fact]
    public async Task Locked_account_refuses_even_the_right_password_until_the_lock_ends()
    {
        var user = await NewUser();
        for (var i = 0; i < User.MaxFailedSignIns; i++)
            await Login(user.UserName, "wrong");

        clock.Now = TestData.Now.AddMinutes(14);
        Problem(await Login(user.UserName, Password), 401, "locked_out");
        Assert.Null(authentication.SignedIn);

        clock.Now = TestData.Now.AddMinutes(15);
        var response = await Login(user.UserName, Password);
        Assert.Equal(user.Id, response.Value!.Id);
        var reloaded = await Reload(user);
        Assert.Equal(0, reloaded.FailedSignInCount);
        Assert.Null(reloaded.LockedUntil);
    }

    [Fact]
    public async Task Logout_signs_out_even_without_a_session()
    {
        var response = await WithAuth(c => c.Logout());

        Assert.IsType<NoContentResult>(response);
        Assert.Equal(1, authentication.SignOuts);
    }

    [Fact]
    public async Task Me_reads_the_user_fresh_from_the_database()
    {
        var user = await NewUser(UserRole.Risk);

        var response = await WithAuth(c => c.Me(CancellationToken.None), Credentials.PrincipalFor(user));

        Assert.Equal(user.Id, response.Value!.Id);
        Assert.Equal(UserRole.Risk, response.Value.Role);
        Assert.Equal(Permissions.For(UserRole.Risk), response.Value.Permissions);
    }

    [Fact]
    public async Task Me_for_a_user_that_no_longer_exists_signs_out_and_is_unauthenticated()
    {
        var ghost = User.Create("ghost", "Ghost", UserRole.Officer, TestData.Now);

        var response = await WithAuth(c => c.Me(CancellationToken.None), Credentials.PrincipalFor(ghost));

        Problem(response, 401, "unauthenticated");
        Assert.Equal(1, authentication.SignOuts);
    }

    private async Task<User> Deactivated()
    {
        var user = await NewUser();
        await using var db = database.CreateContext();
        var tracked = await db.Users.SingleAsync(u => u.Id == user.Id);
        tracked.Deactivate();
        await db.SaveChangesAsync();
        return tracked;
    }

    [Fact]
    public async Task A_deactivated_user_gets_the_wrong_password_answer_without_a_failure_counted()
    {
        var user = await Deactivated();

        var response = await Login(user.UserName, Password);

        Problem(response, 401, "invalid_credentials");
        Assert.Null(authentication.SignedIn);
        Assert.Equal(0, (await Reload(user)).FailedSignInCount);
    }

    [Fact]
    public async Task Me_says_whether_the_password_must_be_changed()
    {
        var user = await NewUser();
        await using (var db = database.CreateContext())
        {
            (await db.Users.SingleAsync(u => u.Id == user.Id)).ResetPassword(Credentials.Hash(user, Password));
            await db.SaveChangesAsync();
        }
        var fresh = await Reload(user);

        var response = await WithAuth(c => c.Me(CancellationToken.None), Credentials.PrincipalFor(fresh));

        Assert.True(response.Value!.MustChangePassword);
    }

    [Fact]
    public async Task Changing_the_password_needs_the_current_one()
    {
        var user = await NewUser();

        var response = await WithAuth(
            c => c.ChangePassword(new ChangePasswordRequest { CurrentPassword = "wrong", NewPassword = "a new password 1" }, CancellationToken.None),
            Credentials.PrincipalFor(user));

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(response).Value);
        Assert.Equal(["wrong_password"], TestServices.FieldCodes(problem, "currentPassword"));
        Assert.Equal(0, (await Reload(user)).FailedSignInCount);
        Assert.True(Credentials.Verify(await Reload(user), Password));
    }

    [Fact]
    public async Task Changing_the_password_keeps_this_session_and_ends_the_others()
    {
        var user = await NewUser();

        var response = await WithAuth(
            c => c.ChangePassword(new ChangePasswordRequest { CurrentPassword = Password, NewPassword = "a new password 1" }, CancellationToken.None),
            Credentials.PrincipalFor(user));

        Assert.IsType<NoContentResult>(response);
        var stored = await Reload(user);
        Assert.True(Credentials.Verify(stored, "a new password 1"));
        Assert.False(stored.MustChangePassword);
        Assert.NotEqual(user.SecurityStamp, stored.SecurityStamp);
        Assert.Equal(SessionCheck.Valid, Credentials.Check(authentication.SignedIn!, stored));
        Assert.Equal(SessionCheck.Reject, Credentials.Check(Credentials.PrincipalFor(user), stored));
    }

    [Fact]
    public async Task Users_set_their_own_language()
    {
        var user = await NewUser();

        var response = await WithAuth(
            c => c.SetLanguage(new LanguageRequest { Language = Languages.Serbian }, CancellationToken.None),
            Credentials.PrincipalFor(user));

        Assert.Equal("sr", response.Value!.PreferredLanguage);
        Assert.Equal("sr", (await Reload(user)).PreferredLanguage);
    }
}
