using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Controllers;
using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class ModelsControllerTests(DatabaseFixture database) : IAsyncLifetime
{
    private const string Challenger = "credit_lr_isotonic_v1";

    private static readonly MlModelInfo ChallengerInfo = new(
        Challenger, "LogisticRegression", TestData.Dataset, 0.3, 0.15, new DateTimeOffset(2026, 11, 1, 19, 0, 0, TimeSpan.Zero));

    private readonly FakeMlClient ml = new() { Catalog = new MlCatalog([ChallengerInfo]) };
    private readonly ManualTimeProvider clock = new(TestData.Now);

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<T> WithController<T>(Func<ModelsController, Task<T>> action)
    {
        await using var db = database.CreateContext();
        var controller = new ModelsController(db, ml, clock)
        {
            ControllerContext = TestServices.SignedIn(database.Officer),
        };
        return await action(controller);
    }

    private Task<ActionResult<ScoringModelResponse>> Register(string modelId = Challenger) =>
        WithController(c => c.Register(new RegisterModelRequest { ModelId = modelId }, CancellationToken.None));

    private async Task<List<ScoringModelResponse>> Registered() =>
        (await WithController(c => c.List(CancellationToken.None))).ToList();

    [Fact]
    public async Task Migration_seeds_the_random_forest_as_the_active_model()
    {
        var model = Assert.Single(await Registered());

        Assert.Equal((TestData.ModelId, ModelRole.Active, TestData.Threshold), (model.ModelId, model.Role, model.Threshold));
    }

    [Fact]
    public async Task Database_refuses_a_second_active_model()
    {
        await using var db = database.CreateContext();
        var (model, _) = ModelRegistry.Register(Challenger, "LogisticRegression", TestData.Dataset, 0.3, 0.15, database.Officer.AsActor(), TestData.Now);
        ModelRegistry.Promote([model], Challenger, "second", database.Officer.AsActor(), TestData.Now);
        db.ScoringModels.Add(model);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());

        Assert.True(Pg.IsExclusionViolation(ex), ex.ToString());
    }

    [Fact]
    public async Task Registering_takes_the_threshold_from_the_catalog_and_logs_who_did_it()
    {
        var created = Assert.IsType<CreatedAtActionResult>((await Register()).Result);

        var model = Assert.IsType<ScoringModelResponse>(created.Value);
        Assert.Equal((Challenger, "LogisticRegression", ModelRole.Shadow, 0.3, 0.15), (model.ModelId, model.DisplayName, model.Role, model.Threshold, model.ReviewFrom));
        await using var db = database.CreateContext();
        var logged = await db.ModelEvents.SingleAsync();
        Assert.Equal((Challenger, (ModelRole?)null, ModelRole.Shadow, database.Officer.Id),
            (logged.ModelId, logged.FromRole, logged.ToRole, logged.ChangedByUserId));
    }

    [Fact]
    public async Task Model_the_service_does_not_serve_cannot_be_registered()
    {
        var response = await Register("credit_xgb_v9");

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(response.Result).Value);
        Assert.Equal(["modelId"], problem.Errors.Keys);
        Assert.Equal(["model_not_served"], TestServices.FieldCodes(problem, "modelId"));
    }

    [Fact]
    public async Task Registering_twice_is_a_conflict()
    {
        await Register();

        var conflict = Assert.IsAssignableFrom<ObjectResult>((await Register()).Result);
        Assert.Equal((409, "conflict"), (conflict.StatusCode, Assert.IsType<ProblemDetails>(conflict.Value).Extensions["code"]));
    }

    [Fact]
    public async Task Registering_while_the_catalog_is_unavailable_is_service_unavailable()
    {
        ml.Catalog = new MlCatalogUnavailable(MlErrorCodes.Unreachable, "down");

        var result = Assert.IsType<ObjectResult>((await Register()).Result);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("inference_unavailable", Assert.IsType<ProblemDetails>(result.Value).Extensions["code"]);
    }

    [Fact]
    public async Task Available_lists_only_models_not_yet_registered()
    {
        ml.Catalog = new MlCatalog([ChallengerInfo, ChallengerInfo with { ModelId = TestData.ModelId }]);

        var available = Assert.IsType<OkObjectResult>((await WithController(c => c.Available(CancellationToken.None))).Result);

        var model = Assert.Single(Assert.IsAssignableFrom<IEnumerable<AvailableModelResponse>>(available.Value));
        Assert.Equal(Challenger, model.ModelId);
    }

    [Fact]
    public async Task Promoting_swaps_the_active_model_and_logs_both_changes()
    {
        await Register();
        clock.Now = TestData.Now.AddMinutes(1);

        var response = await WithController(c =>
            c.Promote(Challenger, new ChangeModelRoleRequest { Reason = "lower cost" }, CancellationToken.None));

        Assert.IsType<OkObjectResult>(response.Result);
        var roles = (await Registered()).ToDictionary(m => m.ModelId, m => m.Role);
        Assert.Equal(ModelRole.Active, roles[Challenger]);
        Assert.Equal(ModelRole.Shadow, roles[TestData.ModelId]);
        var events = await WithController(c => c.Events(CancellationToken.None));
        Assert.Equal(3, events.Count);
        Assert.All(events.Take(2), e => Assert.Equal("lower cost", e.Reason));
    }

    [Fact]
    public async Task Promoting_without_a_reason_is_a_validation_problem()
    {
        await Register();

        var response = await WithController(c =>
            c.Promote(Challenger, new ChangeModelRoleRequest(), CancellationToken.None));

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(response.Result).Value);
        Assert.Equal(["reason"], problem.Errors.Keys);
        Assert.Equal(["reason_required"], TestServices.FieldCodes(problem, "reason"));
    }

    [Fact]
    public async Task Retiring_the_active_model_is_refused_and_unknown_models_are_not_found()
    {
        var active = await WithController(c =>
            c.Retire(TestData.ModelId, new ChangeModelRoleRequest { Reason = "old" }, CancellationToken.None));
        var unknown = await WithController(c =>
            c.Retire("nope", new ChangeModelRoleRequest { Reason = "x" }, CancellationToken.None));

        Assert.IsType<BadRequestObjectResult>(active.Result);
        Assert.IsType<NotFoundResult>(unknown.Result);
    }

    [Fact]
    public async Task Retired_model_stays_listed_with_its_role()
    {
        await Register();

        await WithController(c => c.Retire(Challenger, new ChangeModelRoleRequest { Reason = "worse" }, CancellationToken.None));

        Assert.Equal(ModelRole.Retired, (await Registered()).Single(m => m.ModelId == Challenger).Role);
    }
}
