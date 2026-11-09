using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Controllers;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class UsersControllerTests(DatabaseFixture database) : IAsyncLifetime
{
    private const string Temporary = "temporary password 1";

    private readonly ManualTimeProvider clock = new(TestData.Now);
    private User admin = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        admin = await NewUser(UserRole.Admin, "Vesna Admin");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<User> NewUser(UserRole role = UserRole.Officer, string displayName = "Ana Jovanović")
    {
        var user = User.Create($"u-{Guid.NewGuid():N}"[..20], displayName, role, TestData.Now);
        user.SetPasswordHash(Credentials.Hash(user, "old password 123"));
        await using var db = database.CreateContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<T> AsAdmin<T>(Func<UsersController, Task<T>> action)
    {
        await using var db = database.CreateContext(admin.AsActor());
        return await action(new UsersController(db, clock) { ControllerContext = TestServices.SignedIn(admin) });
    }

    private async Task<User> Reload(Guid id)
    {
        await using var db = database.CreateContext();
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
    }

    private static CreateUserRequest NewRequest(string userName = "Marko.Ilic") => new()
    {
        UserName = userName,
        DisplayName = "Marko Ilić",
        Email = "marko@bank.rs",
        Role = UserRole.Manager,
        PreferredLanguage = Languages.Serbian,
        TemporaryPassword = Temporary,
    };

    private static UpdateUserRequest Edit(User user, UserRole? role = null) => new()
    {
        DisplayName = user.DisplayName,
        Email = user.Email,
        Role = role ?? user.Role,
        PreferredLanguage = user.PreferredLanguage,
    };

    private static ProblemDetails Problem(IActionResult result, int status, string code)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Equal(code, problem.Extensions["code"]);
        return problem;
    }

    [Fact]
    public async Task List_returns_every_user_by_display_name_with_lock_state()
    {
        var locked = await NewUser(displayName: "Ana Locked");
        await using (var db = database.CreateContext())
        {
            var tracked = await db.Users.SingleAsync(u => u.Id == locked.Id);
            for (var i = 0; i < User.MaxFailedSignIns; i++)
                tracked.RecordFailedSignIn(TestData.Now);
            await db.SaveChangesAsync();
        }

        var users = await AsAdmin(c => c.List(CancellationToken.None));

        Assert.Equal(users.Select(u => u.DisplayName).Order(StringComparer.Ordinal), users.Select(u => u.DisplayName));
        Assert.Equal(TestData.Now + User.LockoutDuration, users.Single(u => u.Id == locked.Id).LockedUntil);
        Assert.Null(users.Single(u => u.Id == admin.Id).LockedUntil);
    }

    [Fact]
    public async Task An_expired_lock_is_not_shown()
    {
        var user = await NewUser();
        await using (var db = database.CreateContext())
        {
            var tracked = await db.Users.SingleAsync(u => u.Id == user.Id);
            for (var i = 0; i < User.MaxFailedSignIns; i++)
                tracked.RecordFailedSignIn(TestData.Now);
            await db.SaveChangesAsync();
        }
        clock.Now += User.LockoutDuration;

        var shown = Assert.IsType<UserResponse>((await AsAdmin(c => c.Get(user.Id, CancellationToken.None))).Value);

        Assert.Null(shown.LockedUntil);
    }

    [Fact]
    public async Task Unknown_user_is_not_found()
    {
        var response = await AsAdmin(c => c.Get(Guid.NewGuid(), CancellationToken.None));

        Problem(response.Result!, 404, "not_found");
    }

    [Fact]
    public async Task Create_stores_a_user_who_must_change_the_temporary_password()
    {
        var response = await AsAdmin(c => c.Create(NewRequest(), CancellationToken.None));

        var created = Assert.IsType<CreatedAtActionResult>(response.Result);
        var body = Assert.IsType<UserResponse>(created.Value);
        Assert.Equal(("marko.ilic", "Marko Ilić", "marko@bank.rs", UserRole.Manager, "sr"),
            (body.UserName, body.DisplayName, body.Email, body.Role, body.PreferredLanguage));
        Assert.True(body.IsActive);
        Assert.True(body.MustChangePassword);
        Assert.Equal(TestData.Now, body.CreatedAt);
        var stored = await Reload(body.Id);
        Assert.True(Credentials.Verify(stored, Temporary));
    }

    [Fact]
    public async Task A_taken_user_name_is_a_field_error_whatever_its_case()
    {
        await AsAdmin(c => c.Create(NewRequest(), CancellationToken.None));

        var response = await AsAdmin(c => c.Create(NewRequest(" MARKO.ILIC "), CancellationToken.None));

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(response.Result).Value);
        Assert.Equal(["user_name_taken"], TestServices.FieldCodes(problem, "userName"));
    }

    [Fact]
    public async Task Update_changes_the_editable_fields()
    {
        var user = await NewUser();

        var response = await AsAdmin(c => c.Update(user.Id, new UpdateUserRequest
        {
            DisplayName = "Ana Marić",
            Email = "ana@bank.rs",
            Role = UserRole.Manager,
            PreferredLanguage = Languages.Serbian,
        }, CancellationToken.None));

        var body = Assert.IsType<UserResponse>(response.Value);
        Assert.Equal(("Ana Marić", "ana@bank.rs", UserRole.Manager, "sr"), (body.DisplayName, body.Email, body.Role, body.PreferredLanguage));
        Assert.Equal(UserRole.Manager, (await Reload(user.Id)).Role);
    }

    [Fact]
    public async Task Admins_cannot_change_their_own_role()
    {
        var response = await AsAdmin(c => c.Update(admin.Id, Edit(admin, UserRole.Risk), CancellationToken.None));

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(response.Result).Value);
        Assert.Equal(["own_role"], TestServices.FieldCodes(problem, "role"));
        Assert.Equal(UserRole.Admin, (await Reload(admin.Id)).Role);
    }

    [Fact]
    public async Task Updating_an_unknown_user_is_not_found()
    {
        var response = await AsAdmin(c => c.Update(Guid.NewGuid(), Edit(admin), CancellationToken.None));

        Problem(response.Result!, 404, "not_found");
    }

    [Fact]
    public async Task Deactivate_and_reactivate()
    {
        var user = await NewUser();

        var off = Assert.IsType<UserResponse>((await AsAdmin(c => c.Deactivate(user.Id, CancellationToken.None))).Value);
        var stored = await Reload(user.Id);
        var on = Assert.IsType<UserResponse>((await AsAdmin(c => c.Reactivate(user.Id, CancellationToken.None))).Value);

        Assert.False(off.IsActive);
        Assert.NotEqual(user.SecurityStamp, stored.SecurityStamp);
        Assert.True(on.IsActive);
    }

    [Fact]
    public async Task Admins_cannot_deactivate_themselves()
    {
        var response = await AsAdmin(c => c.Deactivate(admin.Id, CancellationToken.None));

        Problem(response.Result!, 409, "own_account");
        Assert.True((await Reload(admin.Id)).IsActive);
    }

    [Fact]
    public async Task Reset_password_sets_a_temporary_one_and_lifts_the_lock()
    {
        var user = await NewUser();
        await using (var db = database.CreateContext())
        {
            var tracked = await db.Users.SingleAsync(u => u.Id == user.Id);
            for (var i = 0; i < User.MaxFailedSignIns; i++)
                tracked.RecordFailedSignIn(TestData.Now);
            await db.SaveChangesAsync();
        }

        var response = await AsAdmin(c => c.ResetPassword(user.Id, new ResetPasswordRequest { TemporaryPassword = Temporary }, CancellationToken.None));

        Assert.IsType<NoContentResult>(response);
        var stored = await Reload(user.Id);
        Assert.True(Credentials.Verify(stored, Temporary));
        Assert.True(stored.MustChangePassword);
        Assert.False(stored.IsLockedOut(TestData.Now));
        Assert.NotEqual(user.SecurityStamp, stored.SecurityStamp);
    }

    [Fact]
    public async Task Resetting_an_unknown_user_is_not_found()
    {
        var response = await AsAdmin(c => c.ResetPassword(Guid.NewGuid(), new ResetPasswordRequest { TemporaryPassword = Temporary }, CancellationToken.None));

        Problem(response, 404, "not_found");
    }
}
