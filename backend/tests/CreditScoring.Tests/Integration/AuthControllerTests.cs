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
}
