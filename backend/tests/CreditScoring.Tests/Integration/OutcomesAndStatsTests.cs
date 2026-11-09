using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Controllers;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using CreditScoring.Api.Reporting;
using Microsoft.AspNetCore.Mvc;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class OutcomesAndStatsTests(DatabaseFixture database) : IAsyncLifetime
{
    private readonly FakeMlClient ml = new();
    private readonly ManualTimeProvider clock = new(TestData.Now);
    private User risk = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        risk = User.Create($"r-{Guid.NewGuid():N}"[..20], "Rita Risk", UserRole.Risk, TestData.Now);
        await using var db = database.CreateContext();
        db.Users.Add(risk);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<T> Applications<T>(User user, Func<ApplicationsController, Task<T>> action)
    {
        await using var db = database.CreateContext(user.AsActor(), Credentials.BranchScopeOf(Credentials.PrincipalFor(user)));
        return await action(new ApplicationsController(db, TestServices.DecisionService(ml, clock), clock) { ControllerContext = TestServices.SignedIn(user) });
    }

    private async Task<ApplicationResponse> Submit(User officer, double pBad, DateTimeOffset at, FinalDecision? decision = null)
    {
        clock.Now = at;
        ml.Result = new MlScore(pBad, TestData.ModelId);
        Guid clientId;
        await using (var db = database.CreateContext())
        {
            var client = Client.Create("Ana Petrović", null, officer.BranchId!.Value, officer.AsActor(), at);
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            clientId = client.Id;
        }
        var created = Assert.IsType<ApplicationResponse>(Assert.IsType<CreatedAtActionResult>(
            (await Applications(officer, c => c.Create(TestData.ValidRequest() with { ClientId = clientId }, CancellationToken.None))).Result).Value);
        if (decision is { } outcome)
            await Applications(officer, c => c.Decide(created.Id, new RecordDecisionRequest { Outcome = outcome, Reason = "checked" }, CancellationToken.None));
        return created;
    }

    private Task<ActionResult<ApplicationResponse>> Record(Guid id, LoanOutcomeKind outcome) =>
        Applications(risk, c => c.RecordOutcome(id, new RecordOutcomeRequest { Outcome = outcome, Note = "from the servicing report" }, CancellationToken.None));

    private static ProblemDetails Problem(IActionResult? result, int status, string code)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Equal(code, problem.Extensions["code"]);
        return problem;
    }

    [Fact]
    public async Task Risk_records_the_outcome_of_an_issued_loan_in_any_branch()
    {
        var loan = await Submit(database.OtherOfficer, 0.1, TestData.Now, FinalDecision.Approve);

        var recorded = (await Record(loan.Id, LoanOutcomeKind.Defaulted)).Value!;

        Assert.Equal((LoanOutcomeKind.Defaulted, "from the servicing report", "Rita Risk"),
            (recorded.LoanOutcome!.Outcome, recorded.LoanOutcome.Note, recorded.LoanOutcome.RecordedBy));
        Problem((await Record(loan.Id, LoanOutcomeKind.Repaid)).Result, 409, "outcome_recorded");
    }

    [Fact]
    public async Task Rejected_or_undecided_applications_have_no_loan()
    {
        var rejected = await Submit(database.Officer, 0.4, TestData.Now, FinalDecision.Reject);
        var open = await Submit(database.Officer, 0.1, TestData.Now);

        Problem((await Record(rejected.Id, LoanOutcomeKind.Repaid)).Result, 409, "no_loan");
        Problem((await Record(open.Id, LoanOutcomeKind.Repaid)).Result, 409, "no_loan");
    }

    [Fact]
    public async Task The_history_lists_loans_waiting_for_an_outcome()
    {
        var waiting = await Submit(database.Officer, 0.1, TestData.Now, FinalDecision.Approve);
        var defaulted = await Submit(database.Officer, 0.1, TestData.Now.AddMinutes(1), FinalDecision.Approve);
        await Submit(database.Officer, 0.4, TestData.Now.AddMinutes(2), FinalDecision.Reject);
        await Record(defaulted.Id, LoanOutcomeKind.Defaulted);

        async Task<List<Guid>> Ids(LoanOutcomeFilter filter) =>
            (await Applications(risk, c => c.List(new HistoryQuery { LoanOutcome = filter }, CancellationToken.None))).Value!.Items.Select(i => i.Id).ToList();

        Assert.Equal([waiting.Id], await Ids(LoanOutcomeFilter.Pending));
        Assert.Equal([defaulted.Id], await Ids(LoanOutcomeFilter.Defaulted));
        Assert.Empty(await Ids(LoanOutcomeFilter.Repaid));
    }

    private async Task<DashboardStats> Stats(User user, StatsQuery query)
    {
        await using var db = database.CreateContext(user.AsActor(), Credentials.BranchScopeOf(Credentials.PrincipalFor(user)));
        return await new StatsController(db).Get(query, CancellationToken.None);
    }

    [Fact]
    public async Task Stats_from_the_database_cover_weeks_branches_the_previous_period_and_outcomes()
    {
        var monday = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        var repaid = await Submit(database.Officer, 0.05, monday.AddDays(1), FinalDecision.Approve);
        await Submit(database.Officer, 0.4, monday.AddDays(8), FinalDecision.Reject);
        await Submit(database.OtherOfficer, 0.25, monday.AddDays(9));
        await Submit(database.Officer, 0.1, monday.AddDays(-3), FinalDecision.Approve);
        await Record(repaid.Id, LoanOutcomeKind.Repaid);

        var stats = await Stats(risk, new StatsQuery { From = monday, To = monday.AddDays(14) });

        Assert.Equal(3, stats.Applications);
        Assert.Equal([1, 2], stats.Weekly.Select(w => w.Applications));
        Assert.Equal([("HQ", 2), ("OTHER", 1)], stats.ByBranch.Select(b => (b.Code, b.Applications)));
        Assert.Equal((3, 1), (stats.Comparison!.Current.Applications, stats.Comparison.Previous.Applications));
        Assert.Equal(new OutcomeGroup(DecisionPath.AsRecommended, 1, 1, 0, 0.0), stats.OutcomeByDecision.Single(g => g.Path == DecisionPath.AsRecommended));
        Assert.Equal((1, 0.0), (stats.Calibration[0].Loans, stats.Calibration[0].ObservedDefaultRate));
    }

    [Fact]
    public async Task Risk_can_look_at_one_branch_and_a_manager_cannot_look_beyond_their_own()
    {
        await Submit(database.Officer, 0.1, TestData.Now);
        await Submit(database.OtherOfficer, 0.1, TestData.Now);

        Assert.Equal(1, (await Stats(risk, new StatsQuery { BranchId = database.OtherBranchId })).Applications);
        Assert.Equal(0, (await Stats(database.Manager, new StatsQuery { BranchId = database.OtherBranchId })).Applications);
        Assert.Equal(1, (await Stats(database.Manager, new StatsQuery())).Applications);
    }
}
