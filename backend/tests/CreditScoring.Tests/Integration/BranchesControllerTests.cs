using CreditScoring.Api.Contracts;
using CreditScoring.Api.Controllers;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class BranchesControllerTests(DatabaseFixture database) : IAsyncLifetime
{
    private readonly ManualTimeProvider clock = new(TestData.Now);
    private User admin = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        admin = User.Create($"a-{Guid.NewGuid():N}"[..20], "Vesna Admin", UserRole.Admin, TestData.Now);
        await using var db = database.CreateContext();
        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string NewCode() => $"T{Guid.NewGuid():N}"[..10].ToUpperInvariant();

    private async Task<T> AsAdmin<T>(Func<BranchesController, Task<T>> action)
    {
        await using var db = database.CreateContext(admin.AsActor());
        return await action(new BranchesController(db, clock) { ControllerContext = TestServices.SignedIn(admin) });
    }

    private async Task<BranchResponse> Create(string code, string name = "Subotica", string? city = "Subotica")
    {
        var response = await AsAdmin(c => c.Create(new CreateBranchRequest { Code = code, Name = name, City = city }, CancellationToken.None));
        return Assert.IsType<BranchResponse>(Assert.IsType<CreatedAtActionResult>(response.Result).Value);
    }

    private static ProblemDetails Problem(IActionResult result, int status, string code)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Equal(code, problem.Extensions["code"]);
        return problem;
    }

    [Fact]
    public async Task The_migration_created_the_head_office()
    {
        var branches = await AsAdmin(c => c.List(CancellationToken.None));

        var hq = branches.Single(b => b.Code == "HQ");
        Assert.Equal((database.HeadOfficeId, "Head office", true), (hq.Id, hq.Name, hq.IsActive));
    }

    [Fact]
    public async Task List_is_sorted_by_code()
    {
        await Create(NewCode());

        var codes = (await AsAdmin(c => c.List(CancellationToken.None))).Select(b => b.Code).ToList();

        Assert.Equal(codes.Order(StringComparer.Ordinal), codes);
    }

    [Fact]
    public async Task Create_stores_an_upper_case_code()
    {
        var code = NewCode();

        var created = await Create($" {code.ToLowerInvariant()} ");

        Assert.Equal((code, "Subotica", "Subotica", true, TestData.Now), (created.Code, created.Name, created.City, created.IsActive, created.CreatedAt));
        var shown = Assert.IsType<BranchResponse>((await AsAdmin(c => c.Get(created.Id, CancellationToken.None))).Value);
        Assert.Equal(created, shown);
    }

    [Fact]
    public async Task A_taken_code_is_a_field_error()
    {
        var code = NewCode();
        await Create(code);

        var response = await AsAdmin(c => c.Create(new CreateBranchRequest { Code = code.ToLowerInvariant(), Name = "Again" }, CancellationToken.None));

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(response.Result).Value);
        Assert.Equal(["branch_code_taken"], TestServices.FieldCodes(problem, "code"));
    }

    [Fact]
    public async Task Update_changes_name_and_city()
    {
        var created = await Create(NewCode());

        var response = await AsAdmin(c => c.Update(created.Id, new UpdateBranchRequest { Name = "Subotica centar", City = null, Version = created.Version }, CancellationToken.None));

        var body = Assert.IsType<BranchResponse>(response.Value);
        Assert.Equal((created.Code, "Subotica centar", null), (body.Code, body.Name, body.City));
    }

    [Fact]
    public async Task Unknown_branch_is_not_found()
    {
        Problem((await AsAdmin(c => c.Get(Guid.NewGuid(), CancellationToken.None))).Result!, 404, "not_found");
        Problem((await AsAdmin(c => c.Update(Guid.NewGuid(), new UpdateBranchRequest { Name = "X", Version = 0 }, CancellationToken.None))).Result!, 404, "not_found");
        Problem((await AsAdmin(c => c.Deactivate(Guid.NewGuid(), CancellationToken.None))).Result!, 404, "not_found");
    }

    [Fact]
    public async Task A_branch_with_active_users_stays_open()
    {
        var response = await AsAdmin(c => c.Deactivate(database.HeadOfficeId, CancellationToken.None));

        Problem(response.Result!, 409, "branch_has_active_users");
        await using var db = database.CreateContext();
        Assert.True((await db.Branches.SingleAsync(b => b.Id == database.HeadOfficeId)).IsActive);
    }

    [Fact]
    public async Task A_branch_whose_users_are_all_inactive_can_close_and_reopen()
    {
        var created = await Create(NewCode());
        var user = User.Create($"u-{Guid.NewGuid():N}"[..20], "Ana", UserRole.Officer, TestData.Now, created.Id);
        user.Deactivate();
        await using (var db = database.CreateContext())
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var closed = Assert.IsType<BranchResponse>((await AsAdmin(c => c.Deactivate(created.Id, CancellationToken.None))).Value);
        var reopened = Assert.IsType<BranchResponse>((await AsAdmin(c => c.Reactivate(created.Id, CancellationToken.None))).Value);

        Assert.False(closed.IsActive);
        Assert.True(reopened.IsActive);
    }
}
