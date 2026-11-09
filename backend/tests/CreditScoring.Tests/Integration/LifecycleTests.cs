using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Controllers;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using Microsoft.AspNetCore.Mvc;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class LifecycleTests(DatabaseFixture database) : IAsyncLifetime
{
    private readonly FakeMlClient ml = new() { Result = new MlScore(0.1, TestData.ModelId) };
    private readonly ManualTimeProvider clock = new(TestData.Now);
    private User manager = null!;
    private User colleague = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        manager = await NewUser(UserRole.Manager, "Marko Manager", database.HeadOfficeId);
        colleague = await NewUser(UserRole.Officer, "Kaja Colleague", database.HeadOfficeId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<User> NewUser(UserRole role, string name, Guid? branchId = null)
    {
        var user = User.Create($"u-{Guid.NewGuid():N}"[..20], name, role, TestData.Now, branchId);
        await using var db = database.CreateContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<T> As<T>(User user, Func<ApplicationsController, Task<T>> action)
    {
        await using var db = database.CreateContext(user.AsActor(), Credentials.BranchScopeOf(Credentials.PrincipalFor(user)));
        return await action(new ApplicationsController(db, TestServices.DecisionService(ml, clock), clock)
        {
            ControllerContext = TestServices.SignedIn(user),
        });
    }

    private async Task<ApplicationResponse> Submit(int loanAmount = 12_000, double pBad = 0.1)
    {
        ml.Result = new MlScore(pBad, TestData.ModelId);
        Guid clientId;
        await using (var db = database.CreateContext())
        {
            var client = Client.Create("Ana Petrović", null, database.HeadOfficeId, database.Officer.AsActor(), TestData.Now);
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            clientId = client.Id;
        }
        var response = await As(database.Officer, c => c.Create(TestData.ValidRequest() with { ClientId = clientId, LoanAmount = loanAmount }, CancellationToken.None));
        return Assert.IsType<ApplicationResponse>(Assert.IsType<CreatedAtActionResult>(response.Result).Value);
    }

    private static ApplicationResponse Body(ActionResult<ApplicationResponse> response) =>
        response.Value ?? Assert.IsType<ApplicationResponse>(Assert.IsAssignableFrom<ObjectResult>(response.Result).Value);

    private static ProblemDetails Problem(IActionResult? result, int status, string code)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Equal(code, problem.Extensions["code"]);
        return problem;
    }

    private Task<ActionResult<ApplicationResponse>> Decide(User user, Guid id, FinalDecision outcome, string? reason = null) =>
        As(user, c => c.Decide(id, new RecordDecisionRequest { Outcome = outcome, Reason = reason }, CancellationToken.None));

    [Fact]
    public async Task A_submitted_application_gets_a_reference_and_its_submitter_as_owner()
    {
        var first = await Submit();
        var second = await Submit();

        Assert.Matches("^A-[0-9]{6}$", first.Reference);
        Assert.True(string.CompareOrdinal(second.Reference, first.Reference) > 0);
        Assert.Equal(ApplicationStage.Submitted, first.Stage);
        Assert.Equal((database.Officer.Id, "Olga Officer"), (first.AssignedTo!.Id, first.AssignedTo.DisplayName));
        Assert.Empty(first.SignOffs);
    }

    [Fact]
    public async Task A_large_loan_waits_for_a_managers_sign_off_and_then_names_both()
    {
        var submitted = await Submit(loanAmount: 30_000);

        var waiting = Body(await Decide(database.Officer, submitted.Id, FinalDecision.Approve));
        var approved = Body(await As(manager, c => c.SignOff(submitted.Id, new SignOffReviewRequest { Approve = true }, CancellationToken.None)));

        Assert.Equal(ApplicationStage.AwaitingSignOff, waiting.Stage);
        Assert.Null(waiting.OfficerDecision);
        Assert.Equal((SignOffReason.LargeAmount, SignOffStatus.Pending, "Olga Officer"),
            (waiting.SignOffs.Single().Why, waiting.SignOffs.Single().Status, waiting.SignOffs.Single().RequestedBy));
        Assert.Equal(ApplicationStage.Decided, approved.Stage);
        Assert.Equal(("Olga Officer", "Marko Manager"), (approved.OfficerDecision!.DecidedBy, approved.OfficerDecision.SignedOffBy));
        Assert.Equal(SignOffStatus.Approved, approved.SignOffs.Single().Status);
    }

    [Fact]
    public async Task A_returned_sign_off_needs_a_note_and_goes_back_to_the_officer()
    {
        var submitted = await Submit(pBad: 0.4);
        await Decide(database.Officer, submitted.Id, FinalDecision.Approve, "guarantor added");

        var refused = await As(manager, c => c.SignOff(submitted.Id, new SignOffReviewRequest { Approve = false }, CancellationToken.None));
        var returned = Body(await As(manager, c => c.SignOff(submitted.Id, new SignOffReviewRequest { Approve = false, Note = "check the guarantor" }, CancellationToken.None)));

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(refused.Result).Value);
        Assert.Equal(["note_required"], TestServices.FieldCodes(problem, "note"));
        Assert.Equal(ApplicationStage.Submitted, returned.Stage);
        Assert.Equal(("check the guarantor", SignOffStatus.Returned), (returned.SignOffs.Single().Note, returned.SignOffs.Single().Status));
    }

    [Fact]
    public async Task Signing_off_with_nothing_pending_is_a_conflict()
    {
        var submitted = await Submit();

        var response = await As(manager, c => c.SignOff(submitted.Id, new SignOffReviewRequest { Approve = true }, CancellationToken.None));

        Problem(response.Result, 409, "no_pending_sign_off");
    }

    [Fact]
    public async Task Another_officer_cannot_decide_until_it_is_assigned_to_them()
    {
        var submitted = await Submit();

        Problem((await Decide(colleague, submitted.Id, FinalDecision.Approve)).Result, 403, "not_assignee");

        var reassigned = Body(await As(manager, c => c.Assign(submitted.Id, new AssignRequest { UserId = colleague.Id }, CancellationToken.None)));
        Assert.Equal(colleague.Id, reassigned.AssignedTo!.Id);
        Assert.Equal(ApplicationStage.Decided, Body(await Decide(colleague, submitted.Id, FinalDecision.Approve)).Stage);
    }

    [Fact]
    public async Task Assignment_is_only_to_active_staff_of_the_same_branch()
    {
        var submitted = await Submit();
        var risk = await NewUser(UserRole.Risk, "Rita Risk");

        foreach (var userId in new[] { database.OtherOfficer.Id, risk.Id, Guid.NewGuid() })
        {
            var response = await As(manager, c => c.Assign(submitted.Id, new AssignRequest { UserId = userId }, CancellationToken.None));
            var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(response.Result).Value);
            Assert.Equal(["assignee_not_found"], TestServices.FieldCodes(problem, "userId"));
        }
    }

    [Fact]
    public async Task A_withdrawn_application_is_closed()
    {
        var submitted = await Submit();

        var withdrawn = Body(await As(database.Officer, c => c.Withdraw(submitted.Id, new WithdrawRequest { Reason = "client cancelled" }, CancellationToken.None)));

        Assert.Equal(ApplicationStage.Withdrawn, withdrawn.Stage);
        Assert.Equal(("client cancelled", "Olga Officer"), (withdrawn.Withdrawal!.Reason, withdrawn.Withdrawal.By));
        Problem((await Decide(database.Officer, submitted.Id, FinalDecision.Approve)).Result, 409, "not_decidable");
        Problem((await As(database.Officer, c => c.Withdraw(submitted.Id, new WithdrawRequest { Reason = "again" }, CancellationToken.None))).Result, 409, "not_withdrawable");
        Problem((await As(manager, c => c.Assign(submitted.Id, new AssignRequest { UserId = colleague.Id }, CancellationToken.None))).Result, 409, "not_assignable");
    }

    [Fact]
    public async Task Only_the_owner_or_a_manager_withdraws()
    {
        var submitted = await Submit();

        Problem((await As(colleague, c => c.Withdraw(submitted.Id, new WithdrawRequest { Reason = "x" }, CancellationToken.None))).Result, 403, "not_assignee");
        Assert.Equal(ApplicationStage.Withdrawn,
            Body(await As(manager, c => c.Withdraw(submitted.Id, new WithdrawRequest { Reason = "duplicate" }, CancellationToken.None))).Stage);
    }

    [Fact]
    public async Task Notes_are_added_and_listed_oldest_first_within_the_branch()
    {
        var submitted = await Submit();

        await As(database.Officer, c => c.AddNote(submitted.Id, new NoteRequest { Text = "called the employer" }, CancellationToken.None));
        clock.Now = TestData.Now.AddMinutes(5);
        await As(manager, c => c.AddNote(submitted.Id, new NoteRequest { Text = " income confirmed " }, CancellationToken.None));
        var notes = (await As(colleague, c => c.Notes(submitted.Id, CancellationToken.None))).Value!;
        var outside = await As(database.OtherOfficer, c => c.Notes(submitted.Id, CancellationToken.None));

        Assert.Equal([("called the employer", "Olga Officer"), ("income confirmed", "Marko Manager")], notes.Select(n => (n.Text, n.Author)));
        Assert.IsType<NotFoundResult>(outside.Result);
    }

    [Fact]
    public async Task The_history_filters_by_stage()
    {
        var open = await Submit();
        var closed = await Submit();
        await Decide(database.Officer, closed.Id, FinalDecision.Approve);

        var submitted = (await As(database.Officer, c => c.List(new HistoryQuery { Stage = ApplicationStage.Submitted }, CancellationToken.None))).Value!.Items;
        var decided = (await As(database.Officer, c => c.List(new HistoryQuery { Stage = ApplicationStage.Decided }, CancellationToken.None))).Value!.Items;

        Assert.Equal([open.Id], submitted.Select(i => i.Id));
        Assert.Equal([closed.Id], decided.Select(i => i.Id));
        Assert.Equal((closed.Reference, ApplicationStage.Decided, "Olga Officer"), (decided[0].Reference, decided[0].Stage, decided[0].AssignedTo!.DisplayName));
    }
}
