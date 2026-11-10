using System.Text.Json;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Controllers;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class DraftsTests(DatabaseFixture database) : IAsyncLifetime
{
    private readonly FakeMlClient ml = new() { Result = new MlScore(0.1, TestData.ModelId) };
    private readonly ManualTimeProvider clock = new(TestData.Now);
    private Guid clientId;

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        clientId = await NewClient(database.HeadOfficeId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> NewClient(Guid branchId, bool active = true)
    {
        await using var db = database.CreateContext();
        var client = Client.Create("Ana Petrović", null, branchId, database.Officer.AsActor(), TestData.Now);
        if (!active)
            client.Deactivate();
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return client.Id;
    }

    private async Task<T> Drafts<T>(User user, Func<DraftsController, Task<T>> action)
    {
        await using var db = database.CreateContext(user.AsActor(), Credentials.BranchScopeOf(Credentials.PrincipalFor(user)));
        return await action(new DraftsController(db, clock) { ControllerContext = TestServices.SignedIn(user) });
    }

    private async Task<ActionResult<ApplicationResponse>> Submit(User user, CreateApplicationRequest request)
    {
        await using var db = database.CreateContext(user.AsActor(), Credentials.BranchScopeOf(Credentials.PrincipalFor(user)));
        return await new ApplicationsController(db, TestServices.DecisionService(ml, clock), clock)
        {
            ControllerContext = TestServices.SignedIn(user),
        }.Create(request, CancellationToken.None);
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private string Partial(Guid? client = null, int loanAmount = 12_000) =>
        $$"""{ "clientId": "{{client ?? clientId}}", "loanAmount": {{loanAmount}}, "purpose": "Car" }""";

    private async Task<DraftResponse> Start(string? json = null)
    {
        var response = await Drafts(database.Officer, c => c.Create(Json(json ?? Partial()), CancellationToken.None));
        return Assert.IsType<DraftResponse>(Assert.IsType<CreatedAtActionResult>(response.Result).Value);
    }

    private static string[] Codes(IActionResult? result, string field) =>
        TestServices.FieldCodes(Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(result).Value), field);

    [Fact]
    public async Task A_draft_keeps_partial_answers_unvalidated()
    {
        var draft = await Start(Partial(loanAmount: 999_999));

        Assert.Equal((DraftStatus.Open, clientId, "Ana Petrović", TestData.Now), (draft.Status, draft.ClientId, draft.ClientName, draft.CreatedAt));
        Assert.Matches("^C-[0-9]{6}$", draft.ClientNumber);
        Assert.Equal((999_999, LoanPurpose.Car, null), (draft.Answers.LoanAmount, draft.Answers.Purpose, draft.Answers.CreditScore));
        Assert.Null(draft.ApplicationId);
    }

    [Fact]
    public async Task A_draft_needs_a_client_of_the_branch_and_well_typed_answers()
    {
        var other = await NewClient(database.OtherBranchId);
        var inactive = await NewClient(database.HeadOfficeId, active: false);

        Assert.Equal(["required"], Codes((await Drafts(database.Officer, c => c.Create(Json("""{ "loanAmount": 1000 }"""), CancellationToken.None))).Result, "clientId"));
        Assert.Equal(["client_not_found"], Codes((await Drafts(database.Officer, c => c.Create(Json(Partial(other)), CancellationToken.None))).Result, "clientId"));
        Assert.Equal(["client_inactive"], Codes((await Drafts(database.Officer, c => c.Create(Json(Partial(inactive)), CancellationToken.None))).Result, "clientId"));
        Assert.Equal(["invalid_value"], Codes((await Drafts(database.Officer, c => c.Create(Json($$"""{ "clientId": "{{clientId}}", "colour": "red" }"""), CancellationToken.None))).Result, "$.colour"));
        Assert.Equal(["invalid_value"], Codes((await Drafts(database.Officer, c => c.Create(Json($$"""{ "clientId": "{{clientId}}", "loanAmount": "lots" }"""), CancellationToken.None))).Result, "$.loanAmount"));
    }

    [Fact]
    public async Task Drafts_are_private_to_their_author()
    {
        var mine = await Start();
        clock.Now = TestData.Now.AddMinutes(1);
        var newer = await Start();

        var listed = await Drafts(database.Officer, c => c.List(CancellationToken.None));
        var theirs = await Drafts(database.Manager, c => c.List(CancellationToken.None));
        var peek = await Drafts(database.Manager, c => c.Get(mine.Id, CancellationToken.None));

        Assert.Equal([newer.Id, mine.Id], listed.Select(d => d.Id));
        Assert.Empty(theirs);
        Assert.IsType<NotFoundResult>(peek.Result);
    }

    [Fact]
    public async Task Updating_replaces_the_answers()
    {
        var draft = await Start();
        clock.Now = TestData.Now.AddMinutes(10);

        var response = await Drafts(database.Officer, c => c.Update(draft.Id, Json($$"""{ "clientId": "{{clientId}}", "creditScore": 700 }"""), CancellationToken.None));

        var updated = Assert.IsType<DraftResponse>(response.Value);
        Assert.Equal((null, 700, TestData.Now.AddMinutes(10)), (updated.Answers.LoanAmount, updated.Answers.CreditScore, updated.UpdatedAt));
    }

    [Fact]
    public async Task A_discarded_draft_is_closed_and_leaves_the_list()
    {
        var draft = await Start();

        var discarded = Assert.IsType<DraftResponse>((await Drafts(database.Officer, c => c.Discard(draft.Id, CancellationToken.None))).Value);
        var update = await Drafts(database.Officer, c => c.Update(draft.Id, Json(Partial()), CancellationToken.None));

        Assert.Equal(DraftStatus.Discarded, discarded.Status);
        Assert.Empty(await Drafts(database.Officer, c => c.List(CancellationToken.None)));
        var problem = Assert.IsAssignableFrom<ObjectResult>(update.Result);
        Assert.Equal((409, "draft_closed"), (problem.StatusCode, Assert.IsAssignableFrom<ProblemDetails>(problem.Value).Extensions["code"]));
    }

    [Fact]
    public async Task Submitting_with_the_draft_closes_it_once()
    {
        var draft = await Start();
        var request = TestData.ValidRequest() with { ClientId = clientId, DraftId = draft.Id };

        var created = Assert.IsType<ApplicationResponse>(Assert.IsType<CreatedAtActionResult>((await Submit(database.Officer, request)).Result).Value);
        var again = await Submit(database.Officer, request);

        var closed = Assert.IsType<DraftResponse>((await Drafts(database.Officer, c => c.Get(draft.Id, CancellationToken.None))).Value);
        Assert.Equal((DraftStatus.Submitted, created.Id), (closed.Status, closed.ApplicationId));
        Assert.Equal(["draft_closed"], Codes(again.Result, "draftId"));
    }

    [Fact]
    public async Task Someone_elses_draft_cannot_be_submitted()
    {
        var draft = await Start();

        var response = await Submit(database.Manager, TestData.ValidRequest() with { ClientId = clientId, DraftId = draft.Id });

        Assert.Equal(["draft_not_found"], Codes(response.Result, "draftId"));
        Assert.Empty(ml.ScoredModels);
        await using var db = database.CreateContext();
        Assert.False(await db.Applications.AnyAsync());
    }

    private string Complete() =>
        JsonSerializer.Serialize(TestData.ValidRequest() with { ClientId = clientId }, CreditScoring.Api.Contracts.ApiJson.Options);

    [Fact]
    public async Task A_draft_lists_what_would_stop_its_submission()
    {
        var draft = await Start($$"""{ "clientId": "{{clientId}}", "loanAmount": 999999, "publicRecords": 0, "bankruptcies": 1 }""");

        Assert.Equal("range", (string)draft.FieldErrors["loanAmount"].Single()["code"]);
        Assert.Equal(new Dictionary<string, object> { ["code"] = "not_greater_than", ["other"] = "publicRecords" }, draft.FieldErrors["bankruptcies"].Single());
        Assert.Equal("required", (string)draft.FieldErrors["creditScore"].Single()["code"]);
        Assert.False(draft.FieldErrors.ContainsKey("clientId"));
        Assert.False(draft.FieldErrors.ContainsKey("publicRecords"));
    }

    [Fact]
    public async Task A_draft_that_would_pass_has_no_field_errors_in_any_answer()
    {
        var draft = await Start(Complete());

        Assert.Empty(draft.FieldErrors);
        Assert.Empty(Assert.Single(await Drafts(database.Officer, c => c.List(CancellationToken.None))).FieldErrors);
        Assert.Empty((await Drafts(database.Officer, c => c.Get(draft.Id, CancellationToken.None))).Value!.FieldErrors);
        Assert.Empty((await Drafts(database.Officer, c => c.Update(draft.Id, Json(Complete()), CancellationToken.None))).Value!.FieldErrors);
        Assert.Empty((await Drafts(database.Officer, c => c.Discard(draft.Id, CancellationToken.None))).Value!.FieldErrors);
    }

    [Fact]
    public async Task A_draft_for_a_client_with_an_open_application_says_which_one()
    {
        var open = Assert.IsType<ApplicationResponse>(Assert.IsType<CreatedAtActionResult>(
            (await Submit(database.Officer, TestData.ValidRequest() with { ClientId = clientId })).Result).Value);

        var draft = await Start(Complete());

        Assert.Equal(
            new Dictionary<string, object> { ["code"] = "client_has_open_application", ["applicationId"] = open.Id, ["reference"] = open.Reference },
            draft.FieldErrors["clientId"].Single());
    }
}
