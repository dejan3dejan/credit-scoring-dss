using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Controllers;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class ApplicationsControllerTests(DatabaseFixture database) : IAsyncLifetime
{
    private readonly FakeMlClient ml = new();
    private readonly ManualTimeProvider clock = new(TestData.Now);

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<T> WithController<T>(Func<ApplicationsController, Task<T>> action)
    {
        await using var db = database.CreateContext();
        var controller = new ApplicationsController(db, TestServices.DecisionService(ml, clock), clock)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Credentials.PrincipalFor(database.Officer) },
            },
        };
        return await action(controller);
    }

    private async Task<ApplicationResponse> Submit(
        string name,
        MlScoreResult result,
        DateTimeOffset? at = null)
    {
        ml.Result = result;
        clock.Now = at ?? TestData.Now;
        var clientId = await ClientNamed(name);
        var response = await WithController(c =>
            c.Create(TestData.ValidRequest() with { ClientId = clientId }, CancellationToken.None));
        var created = Assert.IsType<CreatedAtActionResult>(response.Result);
        return Assert.IsType<ApplicationResponse>(created.Value);
    }

    private async Task<Guid> ClientNamed(string name)
    {
        await using var db = database.CreateContext();
        var client = Client.Create(name, null, database.Officer.AsActor(), TestData.Now);
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return client.Id;
    }

    [Fact]
    public async Task Unknown_client_is_a_validation_problem_on_client_id()
    {
        var response = await WithController(c =>
            c.Create(TestData.ValidRequest() with { ClientId = Guid.NewGuid() }, CancellationToken.None));

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(response.Result).Value);
        Assert.Equal(["clientId"], problem.Errors.Keys);
    }

    [Fact]
    public async Task Application_takes_the_client_name_as_a_snapshot_and_returns_the_client_number()
    {
        var created = await Submit("Ana Petrovic", new MlScore(0.1, TestData.ModelId));

        await using var db = database.CreateContext();
        var client = await db.Clients.SingleAsync(c => c.Id == created.ClientId);
        Assert.Equal("Ana Petrovic", created.ApplicantName);
        Assert.Equal(client.ClientNumber, created.ClientNumber);
    }

    private async Task<PagedResult<ApplicationSummary>> History(HistoryQuery query)
    {
        var response = await WithController(c => c.List(query, CancellationToken.None));
        return response.Value!;
    }

    private async Task<List<string>> NamesFor(HistoryQuery query) =>
        (await History(query)).Items.Select(i => i.ApplicantName).ToList();

    [Fact]
    public async Task Scored_application_is_saved_with_its_decision()
    {
        var created = await Submit("Ana Petrovic", new MlScore(0.4, TestData.ModelId));

        await using var db = database.CreateContext();
        var saved = await db.Applications.Include(a => a.Decision).SingleAsync(a => a.Id == created.Id);
        Assert.Equal("Ana Petrovic", saved.ApplicantName);
        Assert.Equal(TestData.Now, saved.SubmittedAt);
        Assert.Equal("Olga Officer", saved.SubmittedBy);
        Assert.Equal(database.Officer.Id, saved.SubmittedByUserId);
        Assert.Equal(DecisionStatus.Scored, saved.Decision!.Status);
        Assert.Equal(0.4, saved.Decision.PBad);
        Assert.Equal(TestData.ModelId, saved.Decision.ModelId);
        Assert.Equal(TestData.Threshold, saved.Decision.ThresholdUsed);
        Assert.Equal(TestData.ReviewFrom, saved.Decision.ReviewFromUsed);
        Assert.Equal(Recommendation.Reject, saved.Decision.Recommendation);
        Assert.Null(saved.Decision.ErrorCode);
    }

    [Fact]
    public async Task Score_inside_the_review_band_is_saved_as_a_review_recommendation()
    {
        var created = await Submit("Mira Kovac", new MlScore(0.25, TestData.ModelId));

        Assert.Equal(Recommendation.Review, created.Decision!.Recommendation);
        Assert.Equal(TestData.ReviewFrom, created.Decision.ReviewFromUsed);
        await using var db = database.CreateContext();
        Assert.Equal(Recommendation.Review, (await db.Decisions.SingleAsync(d => d.CreditApplicationId == created.Id)).Recommendation);
    }

    [Fact]
    public async Task Application_is_saved_with_every_answer()
    {
        var created = await Submit("Ana Petrovic", new MlScore(0.1, TestData.ModelId));

        await using var db = database.CreateContext();
        var saved = await db.Applications.Include(a => a.Client).SingleAsync(a => a.Id == created.Id);
        Assert.Equal(TestData.ValidRequest() with { ClientId = saved.ClientId }, CreateApplicationRequest.From(ApplicationResponse.From(saved)));
    }

    [Theory]
    [InlineData(2026, 12, 1)]
    [InlineData(1900, 1, 1)]
    public async Task First_credit_line_outside_a_possible_range_is_a_validation_problem(int year, int month, int day)
    {
        var clientId = await ClientNamed("Future Credit");

        var response = await WithController(c => c.Create(
            TestData.ValidRequest() with { ClientId = clientId, EarliestCreditLine = new DateOnly(year, month, day) },
            CancellationToken.None));

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(response.Result).Value);
        Assert.Equal(["earliestCreditLine"], problem.Errors.Keys);
        Assert.Empty(ml.ScoredModels);
    }

    [Fact]
    public async Task First_credit_line_in_the_submission_month_is_accepted()
    {
        var clientId = await ClientNamed("New Credit");

        var response = await WithController(c => c.Create(
            TestData.ValidRequest() with { ClientId = clientId, EarliestCreditLine = new DateOnly(2026, 11, 30) },
            CancellationToken.None));

        Assert.IsType<CreatedAtActionResult>(response.Result);
        Assert.Equal(0, ml.LastRequest!.CreditHistoryYears);
    }

    [Fact]
    public async Task Application_is_saved_when_scoring_is_unavailable()
    {
        var created = await Submit("Jelena Nikolic", new MlFailure(MlErrorCodes.Unreachable, null, "down", []));

        Assert.Equal(DecisionStatus.ScoringUnavailable, created.Decision!.Status);
        await using var db = database.CreateContext();
        var decision = await db.Decisions.SingleAsync(d => d.CreditApplicationId == created.Id);
        Assert.Equal(DecisionStatus.ScoringUnavailable, decision.Status);
        Assert.Equal(MlErrorCodes.Unreachable, decision.ErrorCode);
        Assert.Null(decision.PBad);
        Assert.Null(decision.Recommendation);
    }

    [Fact]
    public async Task Get_returns_the_saved_application()
    {
        var created = await Submit("Ana Petrovic", new MlScore(0.1, TestData.ModelId));

        var response = await WithController(c => c.Get(created.Id, CancellationToken.None));

        Assert.Equal(created.Id, response.Value!.Id);
        Assert.Equal(Recommendation.Approve, response.Value.Decision!.Recommendation);
    }

    [Fact]
    public async Task Get_unknown_id_is_not_found()
    {
        var response = await WithController(c => c.Get(Guid.NewGuid(), CancellationToken.None));

        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Fact]
    public async Task History_is_newest_first_and_paged()
    {
        await Submit("first", new MlScore(0.1, TestData.ModelId), TestData.Now);
        await Submit("second", new MlScore(0.1, TestData.ModelId), TestData.Now.AddMinutes(1));
        await Submit("third", new MlScore(0.1, TestData.ModelId), TestData.Now.AddMinutes(2));

        var page1 = await History(new HistoryQuery { Page = 1, PageSize = 2 });
        var page2 = await History(new HistoryQuery { Page = 2, PageSize = 2 });

        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(["third", "second"], page1.Items.Select(i => i.ApplicantName));
        Assert.Equal(["first"], page2.Items.Select(i => i.ApplicantName));
    }

    [Fact]
    public async Task Name_filter_is_case_insensitive_contains()
    {
        await Submit("Ana Petrovic", new MlScore(0.1, TestData.ModelId));
        await Submit("Marko Jovanovic", new MlScore(0.1, TestData.ModelId));

        Assert.Equal(["Ana Petrovic"], await NamesFor(new HistoryQuery { ApplicantName = " PETRO " }));
    }

    [Fact]
    public async Task Name_filter_treats_like_wildcards_literally()
    {
        await Submit("100% Sure", new MlScore(0.1, TestData.ModelId));
        await Submit("1000 Sure", new MlScore(0.1, TestData.ModelId));
        await Submit("a_b", new MlScore(0.1, TestData.ModelId));
        await Submit("axb", new MlScore(0.1, TestData.ModelId));
        await Submit(@"back\slash", new MlScore(0.1, TestData.ModelId));

        Assert.Equal(["100% Sure"], await NamesFor(new HistoryQuery { ApplicantName = "100%" }));
        Assert.Equal(["a_b"], await NamesFor(new HistoryQuery { ApplicantName = "a_b" }));
        Assert.Equal([@"back\slash"], await NamesFor(new HistoryQuery { ApplicantName = @"k\s" }));
    }

    [Fact]
    public async Task Status_and_recommendation_filters()
    {
        await Submit("rejected", new MlScore(0.4, TestData.ModelId));
        await Submit("approved", new MlScore(0.1, TestData.ModelId));
        await Submit("review", new MlScore(0.25, TestData.ModelId));
        await Submit("unavailable", new MlFailure(MlErrorCodes.Timeout, null, "x", []));

        Assert.Equal(["unavailable"], await NamesFor(new HistoryQuery { Status = DecisionStatus.ScoringUnavailable }));
        Assert.Equal(["approved"], await NamesFor(new HistoryQuery { Recommendation = Recommendation.Approve }));
        Assert.Equal(["review"], await NamesFor(new HistoryQuery { Recommendation = Recommendation.Review }));
        Assert.Equal(["rejected"], await NamesFor(new HistoryQuery { Recommendation = Recommendation.Reject }));
    }

    [Fact]
    public async Task Date_filters_accept_any_offset_and_to_is_exclusive()
    {
        var noon = TestData.Now;
        await Submit("at noon", new MlScore(0.1, TestData.ModelId), noon);
        await Submit("at one", new MlScore(0.1, TestData.ModelId), noon.AddHours(1));
        var halfPastNoonInBelgrade = noon.AddMinutes(30).ToOffset(TimeSpan.FromHours(1));

        Assert.Equal(["at one"], await NamesFor(new HistoryQuery { From = halfPastNoonInBelgrade }));
        Assert.Equal(["at noon"], await NamesFor(new HistoryQuery { To = noon.AddHours(1) }));
        Assert.Equal(["at one"], await NamesFor(new HistoryQuery { From = noon.AddHours(1), To = noon.AddHours(2) }));
    }

    private Task<ActionResult<ApplicationResponse>> Decide(Guid id, FinalDecision outcome, string? reason = null) =>
        WithController(c => c.Decide(
            id,
            new RecordDecisionRequest { Outcome = outcome, Reason = reason },
            CancellationToken.None));

    [Fact]
    public async Task Officer_decision_is_saved_and_returned()
    {
        var created = await Submit("Ana Petrovic", new MlScore(0.1, TestData.ModelId));
        clock.Now = TestData.Now.AddMinutes(5);

        var response = await Decide(created.Id, FinalDecision.Reject, "income not verified");

        var result = Assert.IsType<CreatedAtActionResult>(response.Result);
        var body = Assert.IsType<ApplicationResponse>(result.Value);
        Assert.Equal(FinalDecision.Reject, body.OfficerDecision!.Outcome);
        Assert.True(body.OfficerDecision.IsOverride);
        await using var db = database.CreateContext();
        var saved = await db.OfficerDecisions.SingleAsync(d => d.CreditApplicationId == created.Id);
        Assert.Equal("Olga Officer", saved.DecidedBy);
        Assert.Equal(database.Officer.Id, saved.DecidedByUserId);
        Assert.Equal("income not verified", saved.Reason);
        Assert.Equal(TestData.Now.AddMinutes(5), saved.DecidedAt);
    }

    [Fact]
    public async Task Second_decision_is_a_conflict()
    {
        var created = await Submit("Ana Petrovic", new MlScore(0.1, TestData.ModelId));
        await Decide(created.Id, FinalDecision.Approve);

        var response = await Decide(created.Id, FinalDecision.Reject, "changed");

        var conflict = Assert.IsType<ConflictObjectResult>(response.Result);
        Assert.Equal(409, Assert.IsType<ProblemDetails>(conflict.Value).Status);
        await using var db = database.CreateContext();
        Assert.Equal(FinalDecision.Approve, (await db.OfficerDecisions.SingleAsync()).Outcome);
    }

    [Fact]
    public async Task Decision_for_unknown_application_is_not_found()
    {
        var response = await Decide(Guid.NewGuid(), FinalDecision.Approve);

        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Fact]
    public async Task Override_without_reason_is_a_validation_problem_on_reason()
    {
        var created = await Submit("Ana Petrovic", new MlScore(0.1, TestData.ModelId));

        var response = await Decide(created.Id, FinalDecision.Reject);

        var bad = Assert.IsType<BadRequestObjectResult>(response.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(bad.Value);
        Assert.Equal(["reason"], problem.Errors.Keys);
        await using var db = database.CreateContext();
        Assert.False(await db.OfficerDecisions.AnyAsync());
    }

    [Fact]
    public async Task Review_case_without_reason_is_a_validation_problem_and_with_one_is_not_an_override()
    {
        var created = await Submit("Mira Kovac", new MlScore(0.25, TestData.ModelId));

        var refused = Assert.IsType<BadRequestObjectResult>((await Decide(created.Id, FinalDecision.Approve)).Result);
        Assert.Equal(["reason"], Assert.IsType<ValidationProblemDetails>(refused.Value).Errors.Keys);

        var response = await Decide(created.Id, FinalDecision.Approve, "pay slips confirm the stated income");
        var body = Assert.IsType<ApplicationResponse>(Assert.IsType<CreatedAtActionResult>(response.Result).Value);
        Assert.False(body.OfficerDecision!.IsOverride);
    }

    [Fact]
    public async Task Review_filter_finds_pending_decided_and_overridden()
    {
        await Submit("pending", new MlScore(0.1, TestData.ModelId), TestData.Now);
        var approved = await Submit("approved", new MlScore(0.1, TestData.ModelId), TestData.Now.AddMinutes(1));
        var rejected = await Submit("rejected", new MlScore(0.4, TestData.ModelId), TestData.Now.AddMinutes(2));
        var overridden = await Submit("overridden", new MlScore(0.4, TestData.ModelId), TestData.Now.AddMinutes(3));
        await Decide(approved.Id, FinalDecision.Approve);
        await Decide(rejected.Id, FinalDecision.Reject);
        await Decide(overridden.Id, FinalDecision.Approve, "guarantor added");

        Assert.Equal(["pending"], await NamesFor(new HistoryQuery { Review = ReviewFilter.Pending }));
        Assert.Equal(["overridden", "approved"], await NamesFor(new HistoryQuery { Review = ReviewFilter.Approved }));
        Assert.Equal(["rejected"], await NamesFor(new HistoryQuery { Review = ReviewFilter.Rejected }));
        Assert.Equal(["overridden"], await NamesFor(new HistoryQuery { Review = ReviewFilter.Override }));

        var summary = (await History(new HistoryQuery { Review = ReviewFilter.Override })).Items.Single();
        Assert.Equal(FinalDecision.Approve, summary.FinalDecision);
        Assert.True(summary.IsOverride);
        Assert.Null((await History(new HistoryQuery { Review = ReviewFilter.Pending })).Items.Single().FinalDecision);
    }

    private async Task RegisterShadow(string modelId)
    {
        await using var db = database.CreateContext();
        var (model, registered) = ModelRegistry.Register(modelId, "LogisticRegression", TestData.Dataset, TestData.Threshold, TestData.ReviewFrom, database.Officer.AsActor(), TestData.Now);
        db.ScoringModels.Add(model);
        db.ModelEvents.Add(registered);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Submitting_stores_the_shadow_scores_beside_the_active_decision()
    {
        const string challenger = "credit_lr_isotonic_v1";
        await RegisterShadow(challenger);
        ml.ResultsByModel[challenger] = new MlScore(0.4, challenger);

        var created = await Submit("Shadowed", new MlScore(0.1, TestData.ModelId));

        Assert.Equal(Recommendation.Approve, created.Decision!.Recommendation);
        var shadows = Assert.IsType<OkObjectResult>((await WithController(c => c.ShadowScores(created.Id, CancellationToken.None))).Result);
        var shadow = Assert.Single(Assert.IsAssignableFrom<IEnumerable<ShadowScoreResponse>>(shadows.Value));
        Assert.Equal((challenger, 0.4, Recommendation.Reject), (shadow.ModelId, shadow.PBad, shadow.Recommendation));
    }

    [Fact]
    public async Task Shadow_scores_for_unknown_application_are_not_found()
    {
        var response = await WithController(c => c.ShadowScores(Guid.NewGuid(), CancellationToken.None));

        Assert.IsType<NotFoundResult>(response.Result);
    }
    private Task<ActionResult<ApplicationResponse>> Rescore(Guid id) =>
        WithController(c => c.Rescore(id, CancellationToken.None));

    [Fact]
    public async Task Scoring_again_replaces_the_failed_score_and_keeps_the_failed_attempt()
    {
        const string challenger = "credit_lr_isotonic_v1";
        await RegisterShadow(challenger);
        ml.ResultsByModel[challenger] = new MlScore(0.4, challenger);
        var created = await Submit("Unscored", new MlFailure(MlErrorCodes.Timeout, null, "x", []));
        ml.Result = new MlScore(0.1, TestData.ModelId);
        ml.ResultsByModel[challenger] = new MlScore(0.05, challenger);
        clock.Now = TestData.Now.AddMinutes(10);

        var response = await Rescore(created.Id);

        var body = Assert.IsType<ApplicationResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal((DecisionStatus.Scored, 0.1, TestData.Now.AddMinutes(10)), (body.Decision!.Status, body.Decision.PBad, body.Decision.EvaluatedAt));
        Assert.Equal([new ScoringFailureResponse(MlErrorCodes.Timeout, TestData.Now)], body.ScoringFailures);

        var fetched = (await WithController(c => c.Get(created.Id, CancellationToken.None))).Value!;
        Assert.Equal(body.ScoringFailures, fetched.ScoringFailures);
        await using var db = database.CreateContext();
        Assert.Equal(DecisionStatus.Scored, (await db.Decisions.SingleAsync(d => d.CreditApplicationId == created.Id)).Status);
        var shadow = await db.ShadowScores.SingleAsync(s => s.CreditApplicationId == created.Id);
        Assert.Equal((0.05, TestData.Now.AddMinutes(10)), (shadow.PBad, shadow.ScoredAt));
    }

    [Fact]
    public async Task Scoring_again_a_scored_application_is_a_conflict()
    {
        var created = await Submit("Scored", new MlScore(0.1, TestData.ModelId));
        ml.Result = new MlScore(0.9, TestData.ModelId);

        var response = await Rescore(created.Id);

        var conflict = Assert.IsType<ConflictObjectResult>(response.Result);
        Assert.Equal("This application already has a score.", Assert.IsType<ProblemDetails>(conflict.Value).Title);
        await using var db = database.CreateContext();
        Assert.Equal(0.1, (await db.Decisions.SingleAsync()).PBad);
        Assert.False(await db.ScoringFailures.AnyAsync());
    }

    [Fact]
    public async Task Scoring_again_after_a_manual_decision_is_a_conflict()
    {
        var created = await Submit("Assessed", new MlFailure(MlErrorCodes.Timeout, null, "x", []));
        await Decide(created.Id, FinalDecision.Reject, "assessed manually");
        ml.Result = new MlScore(0.1, TestData.ModelId);

        var response = await Rescore(created.Id);

        Assert.IsType<ConflictObjectResult>(response.Result);
        await using var db = database.CreateContext();
        Assert.Equal(DecisionStatus.ScoringUnavailable, (await db.Decisions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Scoring_again_an_unknown_application_is_not_found()
    {
        var response = await Rescore(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(response.Result);
    }
}
