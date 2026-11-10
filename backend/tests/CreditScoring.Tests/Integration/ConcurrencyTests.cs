using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Controllers;
using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class ConcurrencyTests(DatabaseFixture database) : IAsyncLifetime
{
    private readonly FakeMlClient ml = new() { Result = new MlScore(0.1, TestData.ModelId) };
    private readonly ManualTimeProvider clock = new(TestData.Now);

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private CreditScoringDbContext Context(User user) =>
        database.CreateContext(user.AsActor(), Credentials.BranchScopeOf(Credentials.PrincipalFor(user)));

    private async Task<T> Applications<T>(User user, Func<ApplicationsController, Task<T>> action)
    {
        await using var db = Context(user);
        return await action(new ApplicationsController(db, TestServices.DecisionService(ml, clock), clock) { ControllerContext = TestServices.SignedIn(user) });
    }

    private async Task<T> Clients<T>(User user, Func<ClientsController, Task<T>> action)
    {
        await using var db = Context(user);
        return await action(new ClientsController(db, clock) { ControllerContext = TestServices.SignedIn(user) });
    }

    private async Task<ClientResponse> NewClient(string name = "Ana Petrović")
    {
        var response = await Clients(database.Officer, c => c.Create(new CreateClientRequest { FullName = name, DateOfBirth = new DateOnly(1990, 5, 1) }, CancellationToken.None));
        return Assert.IsType<ClientResponse>(Assert.IsType<CreatedAtActionResult>(response.Result).Value);
    }

    private Task<ActionResult<ApplicationResponse>> Submit(User officer, Guid clientId) =>
        Applications(officer, c => c.Create(TestData.ValidRequest() with { ClientId = clientId }, CancellationToken.None));

    private static ApplicationResponse Created(ActionResult<ApplicationResponse> response) =>
        Assert.IsType<ApplicationResponse>(Assert.IsType<CreatedAtActionResult>(response.Result).Value);

    private static ProblemDetails Problem(IActionResult? result, int status, string code)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Equal(code, problem.Extensions["code"]);
        return problem;
    }

    [Fact]
    public async Task A_second_open_application_for_the_same_client_is_refused_and_names_the_first()
    {
        var client = await NewClient();
        var first = Created(await Submit(database.Officer, client.Id));
        var scored = ml.ScoredModels.Count;

        var second = await Submit(database.Manager, client.Id);

        var problem = Problem(second.Result, 409, "client_has_open_application");
        Assert.Equal((first.Id, first.Reference), ((Guid)problem.Extensions["applicationId"]!, (string)problem.Extensions["reference"]!));
        Assert.Equal(scored, ml.ScoredModels.Count);
    }

    [Fact]
    public async Task Two_submissions_at_the_same_moment_give_one_application_and_one_conflict()
    {
        var client = await NewClient();
        using var together = new Barrier(2);
        ml.Together = together;

        var results = await Task.WhenAll(Submit(database.Officer, client.Id), Submit(database.Manager, client.Id));

        var created = Created(Assert.Single(results, r => r.Result is CreatedAtActionResult));
        var refused = Problem(Assert.Single(results, r => r.Result is not CreatedAtActionResult).Result, 409, "client_has_open_application");
        Assert.Equal(created.Id, (Guid)refused.Extensions["applicationId"]!);
        await using var db = database.CreateContext();
        Assert.Equal(1, await db.Applications.CountAsync(a => a.ClientId == client.Id));
    }

    [Fact]
    public async Task Once_the_open_application_is_closed_a_new_one_can_be_submitted()
    {
        var client = await NewClient();
        var first = Created(await Submit(database.Officer, client.Id));
        await Applications(database.Officer, c => c.Decide(first.Id, new RecordDecisionRequest { Outcome = FinalDecision.Approve }, CancellationToken.None));
        var second = Created(await Submit(database.Officer, client.Id));
        await Applications(database.Officer, c => c.Withdraw(second.Id, new WithdrawRequest { Reason = "duplicate" }, CancellationToken.None));

        Created(await Submit(database.Officer, client.Id));
    }

    [Fact]
    public async Task The_database_itself_refuses_a_second_open_application()
    {
        var client = await NewClient();
        Created(await Submit(database.Officer, client.Id));
        await using var db = database.CreateContext();
        var stored = await db.Clients.SingleAsync(c => c.Id == client.Id);
        var officer = await db.Users.SingleAsync(u => u.Id == database.Officer.Id);
        var duplicate = TestData.ValidRequest().ToDomain(TestData.Now, officer.AsActor(), stored);
        duplicate.AssignTo(officer);
        db.Applications.Add(duplicate);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.True(Pg.IsUniqueViolation(ex));
    }

    [Fact]
    public async Task A_client_edit_on_an_old_version_is_refused()
    {
        var client = await NewClient();
        var edit = new UpdateClientRequest { FullName = "Ana Marić", DateOfBirth = client.DateOfBirth, Version = client.Version };

        var first = Assert.IsType<ClientResponse>((await Clients(database.Officer, c => c.Update(client.Id, edit, CancellationToken.None))).Value);
        var second = await Clients(database.Manager, c => c.Update(client.Id, edit with { FullName = "Ana Ilić" }, CancellationToken.None));

        Assert.NotEqual(client.Version, first.Version);
        Problem(second.Result, 409, "edited_elsewhere");
        await using var db = database.CreateContext();
        Assert.Equal("Ana Marić", (await db.Clients.SingleAsync(c => c.Id == client.Id)).FullName);
    }

    [Fact]
    public async Task An_old_version_is_refused_even_when_the_values_are_the_same()
    {
        var client = await NewClient();
        var stale = client.Version;
        await Clients(database.Officer, c => c.Update(client.Id, new UpdateClientRequest { FullName = "Ana Marić", DateOfBirth = client.DateOfBirth, Version = stale }, CancellationToken.None));

        var sameValuesOldVersion = await Clients(database.Officer, c => c.Update(client.Id,
            new UpdateClientRequest { FullName = "Ana Marić", DateOfBirth = client.DateOfBirth, Version = stale }, CancellationToken.None));

        Problem(sameValuesOldVersion.Result, 409, "edited_elsewhere");
    }

    [Fact]
    public async Task A_client_must_be_an_adult()
    {
        var response = await Clients(database.Officer, c => c.Create(new CreateClientRequest { FullName = "Mali Perica", DateOfBirth = new DateOnly(2015, 1, 1) }, CancellationToken.None));

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(response.Result).Value);
        Assert.Equal(["too_young"], TestServices.FieldCodes(problem, "dateOfBirth"));
    }

    [Fact]
    public async Task User_and_branch_edits_on_an_old_version_are_refused()
    {
        var admin = User.Create($"a-{Guid.NewGuid():N}"[..20], "Vesna Admin", UserRole.Admin, TestData.Now);
        var branch = Branch.Create($"V{Guid.NewGuid():N}"[..8].ToUpperInvariant(), "Versioned", null, TestData.Now);
        var user = User.Create($"u-{Guid.NewGuid():N}"[..20], "Ana", UserRole.Officer, TestData.Now, database.HeadOfficeId);
        await using (var setup = database.CreateContext())
        {
            setup.AddRange(admin, branch, user);
            await setup.SaveChangesAsync();
        }

        async Task<T> AsAdmin<T>(Func<CreditScoringDbContext, Task<T>> action)
        {
            await using var db = database.CreateContext(admin.AsActor());
            return await action(db);
        }

        var userNow = (await AsAdmin(db => new UsersController(db, clock) { ControllerContext = TestServices.SignedIn(admin) }.Get(user.Id, CancellationToken.None))).Value!;
        var userEdit = new UpdateUserRequest { DisplayName = "Ana M.", Role = UserRole.Officer, BranchId = database.HeadOfficeId, PreferredLanguage = "en", Version = userNow.Version };
        var userFirst = (await AsAdmin(db => new UsersController(db, clock) { ControllerContext = TestServices.SignedIn(admin) }.Update(user.Id, userEdit, CancellationToken.None))).Value!;
        var userSecond = await AsAdmin(db => new UsersController(db, clock) { ControllerContext = TestServices.SignedIn(admin) }.Update(user.Id, userEdit with { DisplayName = "Ana I." }, CancellationToken.None));

        var branchNow = (await AsAdmin(db => new BranchesController(db, clock) { ControllerContext = TestServices.SignedIn(admin) }.Get(branch.Id, CancellationToken.None))).Value!;
        var branchEdit = new UpdateBranchRequest { Name = "Versioned centar", Version = branchNow.Version };
        var branchFirst = (await AsAdmin(db => new BranchesController(db, clock) { ControllerContext = TestServices.SignedIn(admin) }.Update(branch.Id, branchEdit, CancellationToken.None))).Value!;
        var branchSecond = await AsAdmin(db => new BranchesController(db, clock) { ControllerContext = TestServices.SignedIn(admin) }.Update(branch.Id, branchEdit with { Name = "Other" }, CancellationToken.None));

        Assert.NotEqual(userNow.Version, userFirst.Version);
        Problem(userSecond.Result, 409, "edited_elsewhere");
        Assert.NotEqual(branchNow.Version, branchFirst.Version);
        Problem(branchSecond.Result, 409, "edited_elsewhere");
    }

    [Fact]
    public async Task My_week_counts_submissions_approvals_and_overrides()
    {
        var approved = Created(await Submit(database.Officer, (await NewClient("Ana")).Id));
        var overridden = Created(await Submit(database.Officer, (await NewClient("Mira")).Id));
        Created(await Submit(database.Officer, (await NewClient("Nina")).Id));
        Created(await Submit(database.Manager, (await NewClient("Lena")).Id));
        await Applications(database.Officer, c => c.Decide(approved.Id, new RecordDecisionRequest { Outcome = FinalDecision.Approve }, CancellationToken.None));
        await Applications(database.Officer, c => c.Decide(overridden.Id, new RecordDecisionRequest { Outcome = FinalDecision.Reject, Reason = "employer closed" }, CancellationToken.None));

        await using var db = Context(database.Officer);
        var counts = (await new WorkController(db, clock) { ControllerContext = TestServices.SignedIn(database.Officer) }.Mine(CancellationToken.None)).Counts;

        Assert.Equal((3, 2, 1, 1), (counts.SubmittedLast7Days, counts.DecidedLast7Days, counts.ApprovedLast7Days, counts.OverridesLast7Days));
    }

    [Fact]
    public void Edits_require_the_version_they_were_read_at()
    {
        foreach (var type in new[] { typeof(UpdateClientRequest), typeof(UpdateUserRequest), typeof(UpdateBranchRequest) })
        {
            var version = type.GetProperty("Version")!;
            Assert.Equal(typeof(uint?), version.PropertyType);
            Assert.NotNull(version.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), true).SingleOrDefault());
        }
    }
}
