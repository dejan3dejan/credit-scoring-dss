using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Controllers;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using CreditScoring.Api.Reporting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class StatsControllerTests(DatabaseFixture database) : IAsyncLifetime
{
    private const string Challenger = "credit_lr_isotonic_v1";

    private readonly FakeMlClient ml = new();
    private readonly ManualTimeProvider clock = new(TestData.Now);

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private ControllerContext SignedIn() => new()
    {
        HttpContext = new DefaultHttpContext { User = Credentials.PrincipalFor(database.Officer) },
    };

    private async Task<Guid> Submit(MlScoreResult active, MlScoreResult shadow, DateTimeOffset at)
    {
        ml.Result = active;
        ml.ResultsByModel[Challenger] = shadow;
        clock.Now = at;
        await using var db = database.CreateContext();
        var client = Client.Create("Stats Client", null, database.Officer.AsActor(), at);
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var controller = new ApplicationsController(db, TestServices.DecisionService(ml, clock), clock) { ControllerContext = SignedIn() };
        var response = await controller.Create(TestData.ValidRequest() with { ClientId = client.Id }, CancellationToken.None);
        return ((ApplicationResponse)((CreatedAtActionResult)response.Result!).Value!).Id;
    }

    private async Task Decide(Guid id, FinalDecision outcome, string? reason = null)
    {
        await using var db = database.CreateContext();
        var controller = new ApplicationsController(db, TestServices.DecisionService(ml, clock), clock) { ControllerContext = SignedIn() };
        await controller.Decide(id, new RecordDecisionRequest { Outcome = outcome, Reason = reason }, CancellationToken.None);
    }

    private async Task<DashboardStats> Stats(StatsQuery query)
    {
        await using var db = database.CreateContext();
        return await new StatsController(db).Get(query, CancellationToken.None);
    }

    [Fact]
    public async Task Stats_are_computed_from_stored_applications_in_the_range()
    {
        await using (var db = database.CreateContext())
        {
            var (model, registered) = ModelRegistry.Register(Challenger, "LogisticRegression", TestData.Dataset, TestData.Threshold, database.Officer.AsActor(), TestData.Now);
            db.ScoringModels.Add(model);
            db.ModelEvents.Add(registered);
            await db.SaveChangesAsync();
        }

        var agreed = await Submit(new MlScore(0.05, TestData.ModelId), new MlScore(0.08, Challenger), TestData.Now);
        var overridden = await Submit(new MlScore(0.45, TestData.ModelId), new MlScore(0.10, Challenger), TestData.Now.AddHours(1));
        await Submit(new MlFailure(MlErrorCodes.Timeout, null, "x", []), new MlScore(0.2, Challenger), TestData.Now.AddHours(2));
        await Submit(new MlScore(0.9, TestData.ModelId), new MlScore(0.9, Challenger), TestData.Now.AddDays(-10));
        await Decide(agreed, FinalDecision.Approve);
        await Decide(overridden, FinalDecision.Approve, "guarantor added");

        var stats = await Stats(new StatsQuery { From = TestData.Now.AddDays(-1), To = TestData.Now.AddDays(1) });

        Assert.Equal((3, 2, 1, 2, 1), (stats.Applications, stats.Scored, stats.Unavailable, stats.Decided, stats.Pending));
        Assert.Equal(new MatrixRow(1, 0, 0), stats.Matrix.Approve);
        Assert.Equal(new MatrixRow(1, 0, 0), stats.Matrix.Reject);
        Assert.Equal(new MatrixRow(0, 0, 1), stats.Matrix.Unavailable);
        Assert.Equal(0.5, stats.OverrideRate);
        Assert.Equal(1.0, stats.LowSideOverrideRate);
        Assert.Equal(1.0 / 3.0, stats.UnavailableRate);
        Assert.Equal(1, stats.PBadHistogram[0].Count);
        Assert.Equal(1, stats.PBadHistogram[4].Count);
        Assert.Equal(0, stats.PBadHistogram[9].Count);
        Assert.Equal([new OfficerStats(database.Officer.DisplayName, 2, 1, 0.5)], stats.Officers);
        Assert.Equal([new ModelAgreement(Challenger, 2, 1, 0.5)], stats.ShadowAgreement);
    }

    [Fact]
    public async Task Without_a_range_every_application_counts()
    {
        await Submit(new MlScore(0.05, TestData.ModelId), new MlScore(0.05, Challenger), TestData.Now);
        await Submit(new MlScore(0.9, TestData.ModelId), new MlScore(0.9, Challenger), TestData.Now.AddDays(-400));

        var stats = await Stats(new StatsQuery());

        Assert.Equal(2, stats.Applications);
        Assert.Empty(stats.ShadowAgreement);
    }
}
